using CommunicationApi.Services;
using CommunicationApi.Services.Telegram;
using CommunicationApp.Tests.Helpers;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class IncomingSmsRouterTests
{
    private readonly FakeSenders _senders = new();

    private IncomingSmsRouter Router => new(_senders);

    [Theory]
    [InlineData("Telegram|123|hello", "hello")]
    [InlineData("telegram|123|hello", "hello")]
    [InlineData(" Telegram |123|hello", "hello")]
    [InlineData("Telegram|123|one|two|three", "one|two|three")]
    public async Task Send_PassesRecipientAndTextToTelegram(string text, string sentText)
    {
        await Router.Send(text);

        Assert.Equal([new SentMessage(Target.Telegram, "123", null, sentText)], _senders.Sent);
    }

    [Theory]
    [InlineData("", "Text is required")]
    [InlineData("   ", "Text is required")]
    [InlineData("hello", "Unknown target 'hello'")]
    [InlineData("Viber|123|hello", "Unknown target 'Viber'")]
    [InlineData("3|general|hello", "Unknown target '3'")]
    [InlineData("Slack|#general|hello", "Only Telegram is supported for now")]
    [InlineData("Teams|19:abc@thread.v2|hello", "Only Telegram is supported for now")]
    [InlineData("Email|a@b.c|Subject|Body", "Only Telegram is supported for now")]
    [InlineData("Telegram|123", "Expected Telegram|recipient|text")]
    [InlineData("Telegram|someone|hello", "Telegram recipient must be a numeric chat id")]
    [InlineData("Telegram||hello", "Telegram recipient must be a numeric chat id")]
    public async Task Send_InvalidText_IsPermanentAndSendsNothing(string text, string expectedMessage)
    {
        var exception = await Assert.ThrowsAsync<PermanentSendException>(() => Router.Send(text));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(0, _senders.Attempts);
    }

    public static TheoryData<Exception> PermanentErrors =>
    [
        new TelegramLoginException("Telegram is not logged in"),
        new ArgumentException("Telegram chat 123 not found")
    ];

    [Theory]
    [MemberData(nameof(PermanentErrors))]
    public async Task Send_KnownPermanentError_IsPermanent(Exception error)
    {
        _senders.Exception = error;

        var exception = await Assert.ThrowsAsync<PermanentSendException>(() => Router.Send("Telegram|123|hello"));

        Assert.Same(error, exception.InnerException);
    }

    [Fact]
    public async Task Send_OtherError_IsPassedThrough()
    {
        _senders.Exception = new InvalidOperationException("Network is down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Router.Send("Telegram|123|hello"));
    }
}
