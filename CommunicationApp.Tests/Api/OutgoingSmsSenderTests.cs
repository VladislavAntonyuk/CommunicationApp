using CommunicationApi.Database;
using CommunicationApi.Services;
using CommunicationApi.Services.Auth;
using CommunicationApp.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class OutgoingSmsSenderTests : ApiTest
{
    [Fact]
    public async Task Send_GoesToCurrentUsersPhone()
    {
        TestTokens.SaveUser(Factory.Services);
        var other = TestTokens.SaveUser(Factory.Services, ApiFactory.OtherPhoneNumber);

        await Send(other.Id, new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Equal(ApiFactory.OtherPhoneNumber, Assert.Single(Factory.Publisher.Sms).To);
    }

    [Fact]
    public async Task Send_OtherUserSwitchedProviderOff_IsStillSent()
    {
        var user = TestTokens.SaveUser(Factory.Services);
        var other = TestTokens.SaveUser(Factory.Services, ApiFactory.OtherPhoneNumber);
        await Save(new ProviderSetting { UserId = other.Id, Target = Target.Telegram, Enabled = false });

        await Send(user.Id, new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Single(Factory.Publisher.Sms);
    }

    [Fact]
    public async Task Send_OtherUserSwitchedChatOn_KeepsDefault()
    {
        var user = TestTokens.SaveUser(Factory.Services);
        var other = TestTokens.SaveUser(Factory.Services, ApiFactory.OtherPhoneNumber);
        await Save(new ChatSetting { UserId = other.Id, Target = Target.Telegram, ChatId = "3", Enabled = true });

        await Send(user.Id, new Message("Telegram|3|hello", "3", ChatKind.Channel));

        Assert.Empty(Factory.Publisher.Sms);
    }

    [Fact]
    public async Task Send_UnknownUser_PublishesNothing()
    {
        await Send(42, new Message("Telegram|1|hello", "1", ChatKind.Private));

        Assert.Empty(Factory.Publisher.Sms);
    }

    private async Task Send(int userId, Message message)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().Set(userId);
        await scope.ServiceProvider.GetRequiredService<OutgoingSmsSender>().Send(Target.Telegram, message);
    }

    private async Task Save(object setting)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
        dbContext.Add(setting);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
