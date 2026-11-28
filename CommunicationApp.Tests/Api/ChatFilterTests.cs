using CommunicationApi.Database;
using CommunicationApp.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class ChatFilterTests : ApiTest
{
    [Theory]
    [InlineData(ChatKind.Private, true)]
    [InlineData(ChatKind.Group, false)]
    [InlineData(ChatKind.Channel, false)]
    public async Task Send_ChatNotSwitched_FollowsDefaultForKind(ChatKind kind, bool published)
    {
        await Send(new Message("Telegram|1|hello", "1", kind));

        Assert.Equal(published ? 1 : 0, Factory.Publisher.Published.Count);
    }

    [Theory]
    [InlineData(ChatKind.Group)]
    [InlineData(ChatKind.Channel)]
    public async Task Send_ChatSwitchedOn_MessageIsPublished(ChatKind kind)
    {
        await SaveChat("1", true);

        await Send(new Message("Telegram|1|hello", "1", kind));

        Assert.Single(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Send_PrivateChatSwitchedOff_MessageIsNotPublished()
    {
        await SaveChat("1", false);

        await Send(new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Send_OtherChatSwitched_DoesNotAffectThisChat()
    {
        await SaveChat("2", true);

        await Send(new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Send_SameChatIdSwitchedForOtherProvider_DoesNotAffectThisProvider()
    {
        await SaveChat("1", true, Target.Slack);

        await Send(new Message("Telegram|1|hello", "1", ChatKind.Channel));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Send_ChatIdWithoutKind_MessageIsNotPublished()
    {
        await Send(new Message("Telegram|1|hello", "1"));

        Assert.Empty(Factory.Publisher.Published);
    }

    [Fact]
    public async Task Send_NoChatId_MessageIsPublished()
    {
        await Send(new Message("Telegram|1|hello"));

        Assert.Single(Factory.Publisher.Published);
    }

    private async Task SaveChat(string chatId, bool enabled, Target target = Target.Telegram)
    {
        var user = TestTokens.SaveUser(Factory.Services);
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
        dbContext.ChatSettings.Add(new ChatSetting { UserId = user.Id, Target = target, ChatId = chatId, Enabled = enabled });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task Send(Message message) => Factory.SendSms(message);
}
