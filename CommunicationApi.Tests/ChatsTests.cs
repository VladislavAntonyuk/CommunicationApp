using System.Net;
using System.Net.Http.Json;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ChatsTests : ApiTest
{
    public ChatsTests()
    {
        Factory.Senders.TelegramChats.AddRange(
        [
            new TelegramChat("1", "Olena", ChatKind.Private),
            new TelegramChat("2", "Family", ChatKind.Group),
            new TelegramChat("3", "News", ChatKind.Channel)
        ]);
    }

    [Fact]
    public async Task Get_NothingSaved_ReturnsChatsWithDefaults()
    {
        var chats = await Get();

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
        var json = await Client.GetStringAsync("/providers/Telegram/chats", TestContext.Current.CancellationToken);

        Assert.Contains("\"kind\":\"Channel\"", json);
    }

    [Fact]
    public async Task Put_SwitchChannelOn_OnlyThatChatChanges()
    {
        await Put("3", true);

        Assert.Equal(new[] { true, false, true }, (await Get()).Select(x => x.Enabled).ToArray());
    }

    [Fact]
    public async Task Put_SwitchPrivateChatOff_ChatIsDisabled()
    {
        await Put("1", false);

        Assert.False((await Get()).Single(x => x.ChatId == "1").Enabled);
    }

    [Fact]
    public async Task Put_SwitchOnThenOff_ChatIsDisabled()
    {
        await Put("3", true);

        await Put("3", false);

        Assert.False((await Get()).Single(x => x.ChatId == "3").Enabled);
    }

    [Fact]
    public async Task Put_SwitchChannelOn_ItsMessagesAreStored()
    {
        await Put("3", true);

        await Client.PostAsJsonAsync("/notify/Telegram", new Message("Telegram|3|hello", "3", ChatKind.Channel), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Telegram|3|hello" }, (await Receive()).Select(x => x.Text).ToArray());
    }

    [Theory]
    [InlineData("Slack")]
    [InlineData("Email")]
    public async Task Get_ProviderWithoutChatSelection_ReturnsBadRequest(string target)
    {
        var response = await Client.GetAsync($"/providers/{target}/chats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal($"Chats cannot be chosen for {target}", problem?.Detail);
    }

    [Fact]
    public async Task Put_ProviderWithoutChatSelection_ReturnsBadRequest()
    {
        var response = await Client.PutAsJsonAsync("/providers/Slack/chats/C1", new ProviderSwitch(true), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_TelegramFails_ReturnsBadGatewayWithReason()
    {
        Factory.Senders.Exception = new InvalidOperationException("Telegram is not logged in");

        var response = await Client.GetAsync("/providers/Telegram/chats", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Telegram is not logged in", problem?.Detail);
    }

    private async Task<List<ReceivedMessage>> Receive()
    {
        return await Client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }

    private async Task<List<ChatStatus>> Get()
    {
        return await Client.GetFromJsonAsync<List<ChatStatus>>("/providers/Telegram/chats", TestContext.Current.CancellationToken) ?? [];
    }

    private async Task Put(string chatId, bool enabled)
    {
        var response = await Client.PutAsJsonAsync($"/providers/Telegram/chats/{chatId}", new ProviderSwitch(enabled), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
