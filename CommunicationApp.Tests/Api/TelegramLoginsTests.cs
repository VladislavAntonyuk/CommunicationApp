using CommunicationApi.Services.Telegram;
using CommunicationApp.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;
using TL;
using Message = Shared.Api.Message;

namespace CommunicationApp.Tests.Api;

public sealed class TelegramLoginsTests : ApiTest
{
    private static readonly DateTime Now = new(2026, 11, 28, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<long, ChatBase> Chats = new()
    {
        [10] = new Channel { id = 10, flags = Channel.Flags.broadcast },
        [20] = new Channel { id = 20, flags = Channel.Flags.megagroup }
    };

    [Fact]
    public void For_SameUser_ReturnsSameLogin()
    {
        var logins = Factory.Services.GetRequiredService<TelegramLogins>();

        Assert.Same(logins.For(1), logins.For(1));
        Assert.NotSame(logins.For(1), logins.For(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void ToSms_FreshMessage_IsSent(int minutesAgo)
    {
        var sms = ToSms(new PeerUser { user_id = 1 }, Now.AddMinutes(-minutesAgo));

        Assert.Equal(new Message("Telegram|1|hello", "1", ChatKind.Private), sms);
    }

    [Fact]
    public void ToSms_OldMessage_IsSkipped()
    {
        Assert.Null(ToSms(new PeerUser { user_id = 1 }, Now.AddMinutes(-6)));
    }

    [Fact]
    public void ToSms_OwnMessage_IsSkipped()
    {
        Assert.Null(ToSms(new PeerUser { user_id = 1 }, Now, TL.Message.Flags.out_));
    }

    [Theory]
    [InlineData(10, ChatKind.Channel)]
    [InlineData(20, ChatKind.Group)]
    [InlineData(30, ChatKind.Group)]
    public void ToSms_ChannelPeer_KindFromChats(long channelId, ChatKind kind)
    {
        Assert.Equal(kind, ToSms(new PeerChannel { channel_id = channelId }, Now)?.ChatKind);
    }

    private static Message? ToSms(Peer peer, DateTime date, TL.Message.Flags flags = default) =>
        TelegramLogins.ToSms(
            new UpdateNewMessage { message = new TL.Message { peer_id = peer, date = date, message = "hello", flags = flags } },
            Chats,
            Now,
            MaxAge);
}
