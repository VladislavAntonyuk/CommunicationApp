using System.Net.Http.Json;
using CommunicationApi.Database;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ChatFilterTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(ChatKind.Private, true)]
    [InlineData(ChatKind.Group, false)]
    [InlineData(ChatKind.Channel, false)]
    public async Task Notify_ChatNotSwitched_FollowsDefaultForKind(ChatKind kind, bool stored)
    {
        var client = _factory.CreateClient();

        await Notify(client, new Message("Telegram|1|hello", "1", kind));

        Assert.Equal(stored ? 1 : 0, (await Receive(client)).Count);
    }

    [Theory]
    [InlineData(ChatKind.Group)]
    [InlineData(ChatKind.Channel)]
    public async Task Notify_ChatSwitchedOn_MessageIsStored(ChatKind kind)
    {
        var client = _factory.CreateClient();
        await SaveChat("1", true);

        await Notify(client, new Message("Telegram|1|hello", "1", kind));

        Assert.Single(await Receive(client));
    }

    [Fact]
    public async Task Notify_PrivateChatSwitchedOff_MessageIsNotStored()
    {
        var client = _factory.CreateClient();
        await SaveChat("1", false);

        await Notify(client, new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_OtherChatSwitched_DoesNotAffectThisChat()
    {
        var client = _factory.CreateClient();
        await SaveChat("2", true);

        await Notify(client, new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_SameChatIdSwitchedForOtherProvider_DoesNotAffectThisProvider()
    {
        var client = _factory.CreateClient();
        await SaveChat("1", true, Target.Slack);

        await Notify(client, new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_ChatIdWithoutKind_MessageIsNotStored()
    {
        var client = _factory.CreateClient();

        await Notify(client, new Message("Telegram|1|hello", "1"));

        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_NoChatId_MessageIsStored()
    {
        var client = _factory.CreateClient();

        await Notify(client, new Message("Slack|U1|hello"), "Slack");

        Assert.Single(await Receive(client));
    }

    private async Task SaveChat(string chatId, bool enabled, Target target = Target.Telegram)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
        dbContext.ChatSettings.Add(new ChatSetting { Target = target, ChatId = chatId, Enabled = enabled });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task Notify(HttpClient client, Message message, string target = "Telegram")
    {
        var response = await client.PostAsJsonAsync($"/notify/{target}", message, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<ReceivedMessage>> Receive(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }
}
