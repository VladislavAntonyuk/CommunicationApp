using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class SendValidationTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

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
    public async Task Send_InvalidText_ReturnsBadRequest(string text, string expectedDetail)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/send", new Message(text), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(expectedDetail, problem?.Detail);
        Assert.Empty(_factory.Senders.Sent);
    }
}
