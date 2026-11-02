using System.Net.Http.Json;
using CommunicationApi.Database;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ChatFilterTests : ApiTest
{
    [Theory]
    [InlineData(ChatKind.Private, true)]
    [InlineData(ChatKind.Group, false)]
    [InlineData(ChatKind.Channel, false)]
    public async Task Notify_ChatNotSwitched_FollowsDefaultForKind(ChatKind kind, bool published)
    {
        await Notify(new Message("Telegram|1|hello", "1", kind));

        Assert.Equal(published ? 1 : 0, Factory.Publisher.Published.Count);
    }

    [Theory]
    [InlineData(ChatKind.Group)]
    [InlineData(ChatKind.Channel)]
    public async Task Notify_ChatSwitchedOn_MessageIsPublished(ChatKind kind)
    {
        await SaveChat("1", true);

        await Notify(new Message("Telegram|1|hello", "1", kind));

        Assert.Single(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_PrivateChatSwitchedOff_MessageIsNotPublished()
    {
        await SaveChat("1", false);

        await Notify(new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_OtherChatSwitched_DoesNotAffectThisChat()
    {
        await SaveChat("2", true);

        await Notify(new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_SameChatIdSwitchedForOtherProvider_DoesNotAffectThisProvider()
    {
        await SaveChat("1", true, Target.Slack);

        await Notify(new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_ChatIdWithoutKind_MessageIsNotPublished()
    {
        await Notify(new Message("Telegram|1|hello", "1"));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Notify_NoChatId_MessageIsPublished()
    {
        await Notify(new Message("Slack|U1|hello"), "Slack");

        Assert.Single(Factory.Publisher.Published);
    }

    private async Task SaveChat(string chatId, bool enabled, Target target = Target.Telegram)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
        dbContext.ChatSettings.Add(new ChatSetting { Target = target, ChatId = chatId, Enabled = enabled });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task Notify(Message message, string target = "Telegram")
    {
        var response = await Client.PostAsJsonAsync($"/notify/{target}", message, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
