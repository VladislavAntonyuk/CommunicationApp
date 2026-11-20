using CommunicationApi.Services;
using CommunicationApi.Services.Telegram;
using Shared;
using SlackNet;

namespace CommunicationApi.Tests;

public sealed class IncomingSmsRouterTests
{
    private readonly FakeSenders _senders = new();

    private IncomingSmsRouter Router => new(_senders, _senders, _senders, _senders);

    [Theory]
    [InlineData("Telegram|123|hello", Target.Telegram, "123", "hello")]
    [InlineData("Teams|19:abc@thread.v2|hello", Target.Teams, "19:abc@thread.v2", "hello")]
    [InlineData("Slack|#general|hello", Target.Slack, "#general", "hello")]
    [InlineData("slack|#general|hello", Target.Slack, "#general", "hello")]
    [InlineData(" Slack |#general|hello", Target.Slack, "#general", "hello")]
    [InlineData("Slack|#general|one|two|three", Target.Slack, "#general", "one|two|three")]
    public async Task Send_PassesRecipientAndTextToTargetService(string text, Target target, string recipient, string sentText)
    {
        await Router.Send(text);

        Assert.Equal([new SentMessage(target, recipient, null, sentText)], _senders.Sent);
    }

    [Theory]
    [InlineData("Email|a@b.c|Subject|Body", "Subject", "Body")]
    [InlineData("Email|a@b.c|Subject|one|two", "Subject", "one|two")]
    public async Task Send_Email_PassesRecipientSubjectAndText(string text, string subject, string sentText)
    {
        await Router.Send(text);

        Assert.Equal([new SentMessage(Target.Email, "a@b.c", subject, sentText)], _senders.Sent);
    }

    [Fact]
    public async Task Send_EmailStatusInDifferentCase_Succeeds()
    {
        _senders.EmailStatus = "Success";

        await Router.Send("Email|a@b.c|Subject|Body");

        Assert.Single(_senders.Sent);
    }

    [Fact]
    public async Task Send_EmailNotSent_IsPermanent()
    {
        _senders.EmailStatus = "error";

        var exception = await Assert.ThrowsAsync<PermanentSendException>(() => Router.Send("Email|a@b.c|Subject|Body"));

        Assert.Equal("Email is not sent: error", exception.Message);
    }

    [Theory]
    [InlineData("", "Text is required")]
    [InlineData("   ", "Text is required")]
    [InlineData("hello", "Unknown target 'hello'")]
    [InlineData("Viber|123|hello", "Unknown target 'Viber'")]
    [InlineData("3|general|hello", "Unknown target '3'")]
    [InlineData("Slack", "Expected Slack|recipient|text")]
    [InlineData("Slack|general", "Expected Slack|recipient|text")]
    [InlineData("slack|general", "Expected Slack|recipient|text")]
    [InlineData(" Teams |someone", "Expected Teams|recipient|text")]
    [InlineData("Telegram|123", "Expected Telegram|recipient|text")]
    [InlineData("Email|a@b.c|subject", "Expected Email|to|subject|text")]
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
        new SlackException(null!),
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => Router.Send("Slack|#general|hello"));
    }
}
