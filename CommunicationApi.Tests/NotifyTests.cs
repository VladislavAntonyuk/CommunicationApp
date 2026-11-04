using System.Net;
using System.Net.Http.Json;
using Shared;

namespace CommunicationApi.Tests;

public sealed class NotifyTests : ApiTest
{
    [Fact]
    public async Task Notify_PublishesMessagesInOrder()
    {
        await Notify("Telegram", "Telegram|1|first");
        await Notify("Slack", "Slack|U1|second");
        await Notify("Telegram", "Telegram|1|third");

        Assert.Equal(
            new[] { "Telegram|1|first", "Slack|U1|second", "Telegram|1|third" },
            Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_UnknownTarget_ReturnsBadRequestAndPublishesNothing()
    {
        var response = await Client.PostAsJsonAsync("/notify/Viber", new Message("Viber|1|hello"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.Publisher.Published);
    }

    private async Task Notify(string target, string text)
    {
        var response = await Client.PostAsJsonAsync($"/notify/{target}", new Message(text), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
