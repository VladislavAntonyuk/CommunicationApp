using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class SendRoutingTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("Telegram|123|hello", Target.Telegram, "123", "hello")]
    [InlineData("Teams|19:abc@thread.v2|hello", Target.Teams, "19:abc@thread.v2", "hello")]
    [InlineData("Slack|#general|hello", Target.Slack, "#general", "hello")]
    [InlineData("slack|#general|hello", Target.Slack, "#general", "hello")]
    [InlineData(" Slack |#general|hello", Target.Slack, "#general", "hello")]
    [InlineData("Slack|#general|one|two|three", Target.Slack, "#general", "one|two|three")]
    public async Task Send_PassesRecipientAndTextToTargetService(string text, Target target, string recipient, string sentText)
    {
        var response = await Send(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new[] { new SentMessage(target, recipient, null, sentText) },
            _factory.Senders.Sent);
    }

    [Theory]
    [InlineData("Email|a@b.c|Subject|Body", "Subject", "Body")]
    [InlineData("Email|a@b.c|Subject|one|two", "Subject", "one|two")]
    public async Task Send_Email_PassesRecipientSubjectAndText(string text, string subject, string sentText)
    {
        var response = await Send(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new[] { new SentMessage(Target.Email, "a@b.c", subject, sentText) },
            _factory.Senders.Sent);
    }

    [Fact]
    public async Task Send_EmailStatusInDifferentCase_ReturnsOk()
    {
        _factory.Senders.EmailStatus = "Success";

        var response = await Send("Email|a@b.c|Subject|Body");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Send_EmailNotSent_ReturnsBadGateway()
    {
        _factory.Senders.EmailStatus = "error";

        var response = await Send("Email|a@b.c|Subject|Body");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("Email is not sent: error", problem?.Detail);
    }

    [Theory]
    [InlineData("Email|a@b.c|Subject|Body")]
    [InlineData("Telegram|123|hello")]
    [InlineData("Teams|19:abc@thread.v2|hello")]
    [InlineData("Slack|#general|hello")]
    public async Task Send_ServiceThrows_ReturnsBadGatewayWithMessage(string text)
    {
        _factory.Senders.Exception = new InvalidOperationException("channel_not_found");

        var response = await Send(text);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal("channel_not_found", problem?.Detail);
    }

    private Task<HttpResponseMessage> Send(string text)
    {
        return _factory.CreateClient().PostAsJsonAsync("/send", new Message(text), TestContext.Current.CancellationToken);
    }
}
