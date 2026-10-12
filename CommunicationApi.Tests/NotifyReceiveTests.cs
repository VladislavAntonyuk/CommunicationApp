using System.Net;
using System.Net.Http.Json;
using Shared;

namespace CommunicationApi.Tests;

public sealed class NotifyReceiveTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Receive_NothingNotified_ReturnsEmpty()
    {
        var client = _factory.CreateClient();

        var messages = await Receive(client);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task Receive_ReturnsNotifiedMessagesInOrder()
    {
        var client = _factory.CreateClient();
        await Notify(client, "Telegram", "Telegram|1|first");
        await Notify(client, "Slack", "Slack|U1|second");
        await Notify(client, "Telegram", "Telegram|1|third");

        var messages = await Receive(client);

        Assert.Equal(
            new[] { "Telegram|1|first", "Slack|U1|second", "Telegram|1|third" },
            messages.Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Receive_ReturnsEachMessageOnlyOnce()
    {
        var client = _factory.CreateClient();
        await Notify(client, "Slack", "Slack|U1|first");
        await Receive(client);
        await Notify(client, "Slack", "Slack|U1|second");

        var messages = await Receive(client);

        Assert.Equal(new[] { "Slack|U1|second" }, messages.Select(x => x.Text).ToArray());
        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_UnknownTarget_ReturnsBadRequestAndStoresNothing()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/notify/Viber", new Message("Viber|1|hello"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Receive(client));
    }

    private static async Task Notify(HttpClient client, string target, string text)
    {
        var response = await client.PostAsJsonAsync($"/notify/{target}", new Message(text), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<Message>> Receive(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<Message>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }
}
