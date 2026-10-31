using System.Net;
using System.Net.Http.Json;
using Shared;

namespace CommunicationApi.Tests;

public sealed class NotifyReceiveTests : ApiTest
{
    [Fact]
    public async Task Receive_NothingNotified_ReturnsEmpty()
    {
        var messages = await Receive();

        Assert.Empty(messages);
    }

    [Fact]
    public async Task Receive_ReturnsNotifiedMessagesInOrder()
    {
        await Notify("Telegram", "Telegram|1|first");
        await Notify("Slack", "Slack|U1|second");
        await Notify("Telegram", "Telegram|1|third");

        var messages = await Receive();

        Assert.Equal(
            new[] { "Telegram|1|first", "Slack|U1|second", "Telegram|1|third" },
            messages.Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Receive_NotAcknowledged_ReturnsMessagesAgain()
    {
        await Notify("Slack", "Slack|U1|first");
        await Receive();

        var messages = await Receive();

        Assert.Equal(new[] { "Slack|U1|first" }, messages.Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Receive_NotAcknowledged_ReturnsThemBeforeNewerMessages()
    {
        await Notify("Slack", "Slack|U1|first");
        await Notify("Slack", "Slack|U1|second");
        var first = (await Receive()).Single(x => x.Text == "Slack|U1|first");
        await Acknowledge([first.Id]);
        await Notify("Slack", "Slack|U1|third");

        var messages = await Receive();

        Assert.Equal(new[] { "Slack|U1|second", "Slack|U1|third" }, messages.Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Acknowledge_MessagesAreNotReturnedAgain()
    {
        await Notify("Slack", "Slack|U1|first");
        await Acknowledge((await Receive()).Select(x => x.Id));
        await Notify("Slack", "Slack|U1|second");

        var messages = await Receive();

        Assert.Equal(new[] { "Slack|U1|second" }, messages.Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Acknowledge_OnlyAcknowledgedMessagesAreRemoved()
    {
        await Notify("Slack", "Slack|U1|first");
        await Notify("Slack", "Slack|U1|second");
        var first = (await Receive())[0];

        await Acknowledge([first.Id]);

        Assert.Equal(new[] { "Slack|U1|second" }, (await Receive()).Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Acknowledge_UnknownId_SucceedsAndChangesNothing()
    {
        await Notify("Slack", "Slack|U1|first");

        await Acknowledge(["12345"]);

        Assert.Single(await Receive());
    }

    [Fact]
    public async Task Notify_UnknownTarget_ReturnsBadRequestAndStoresNothing()
    {
        var response = await Client.PostAsJsonAsync("/notify/Viber", new Message("Viber|1|hello"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Receive());
    }

    private async Task Notify(string target, string text)
    {
        var response = await Client.PostAsJsonAsync($"/notify/{target}", new Message(text), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<List<ReceivedMessage>> Receive()
    {
        return await Client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }

    private async Task Acknowledge(IEnumerable<string> ids)
    {
        var response = await Client.PostAsJsonAsync("/receive/ack", new Acknowledgement(ids.ToList()), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
