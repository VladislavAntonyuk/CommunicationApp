using CommunicationApi.Services.Telegram;
using CommunicationApp.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace CommunicationApp.Tests.Api;

public sealed class TelegramLoginsTests : ApiTest
{
    [Fact]
    public void For_SameUser_ReturnsSameLogin()
    {
        var logins = Factory.Services.GetRequiredService<TelegramLogins>();

        Assert.Same(logins.For(1), logins.For(1));
        Assert.NotSame(logins.For(1), logins.For(2));
    }
}
