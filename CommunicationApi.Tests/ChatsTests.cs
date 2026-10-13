using System.Net;
using System.Net.Http.Json;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ChatsTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public ChatsTests()
    {
        _factory.Senders.TelegramChats.AddRange(
        [
            new TelegramChat("1", "Olena", ChatKind.Private),
            new TelegramChat("2", "Family", ChatKind.Group),
            new TelegramChat("3", "News", ChatKind.Channel)
        ]);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_NothingSaved_ReturnsChatsWithDefaults()
    {
        var client = _factory.CreateClient();

        var chats = await Get(client);

        Assert.Equal(
            new[]
            {
                new ChatStatus("1", "Olena", ChatKind.Private, true),
                new ChatStatus("2", "Family", ChatKind.Group, false),
                new ChatStatus("3", "News", ChatKind.Channel, false)
            },
            chats.ToArray());
    }

    [Fact]
    public async Task Get_ReturnsKindAsName()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/providers/Telegram/chats", TestContext.Current.CancellationToken);

        Assert.Contains("\"kind\":\"Channel\"", json);
    }

    [Fact]
    public async Task Put_SwitchChannelOn_OnlyThatChatChanges()
    {
        var client = _factory.CreateClient();

        await Put(client, "3", true);

        Assert.Equal(new[] { true, false, true }, (await Get(client)).Select(x => x.Enabled).ToArray());
    }

    [Fact]
    public async Task Put_SwitchPrivateChatOff_ChatIsDisabled()
    {
        var client = _factory.CreateClient();

        await Put(client, "1", false);

        Assert.False((await Get(client)).Single(x => x.ChatId == "1").Enabled);
    }

    [Fact]
    public async Task Put_SwitchOnThenOff_ChatIsDisabled()
    {
        var client = _factory.CreateClient();
        await Put(client, "3", true);

        await Put(client, "3", false);

        Assert.False((await Get(client)).Single(x => x.ChatId == "3").Enabled);
    }

    [Fact]
    public async Task Put_SwitchChannelOn_ItsMessagesAreStored()
    {
        var client = _factory.CreateClient();
        await Put(client, "3", true);

        await client.PostAsJsonAsync("/notify/Telegram", new Message("Telegram|3|hello", "3", ChatKind.Channel), TestContext.Current.CancellationToken);

        var messages = await client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Telegram|3|hello" }, messages?.Select(x => x.Text).ToArray());
    }

    [Theory]
    [InlineData("Slack")]
    [InlineData("Email")]
    public async Task Get_ProviderWithoutChatSelection_ReturnsBadRequest(string target)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/providers/{target}/chats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal($"Chats cannot be chosen for {target}", problem?.Detail);
    }

    [Fact]
    public async Task Put_ProviderWithoutChatSelection_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/providers/Slack/chats/C1", new ProviderSwitch(true), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_TelegramFails_ReturnsBadGatewayWithReason()
    {
        _factory.Senders.Exception = new InvalidOperationException("Telegram is not logged in");
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/providers/Telegram/chats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Telegram is not logged in", problem?.Detail);
    }

    private static async Task<List<ChatStatus>> Get(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<ChatStatus>>("/providers/Telegram/chats", TestContext.Current.CancellationToken) ?? [];
    }

    private static async Task Put(HttpClient client, string chatId, bool enabled)
    {
        var response = await client.PutAsJsonAsync($"/providers/Telegram/chats/{chatId}", new ProviderSwitch(enabled), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
