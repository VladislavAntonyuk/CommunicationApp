using System.Net;
using System.Net.Http.Json;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class TelegramLoginTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_ReturnsCurrentStep()
    {
        _factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.LoggedIn, "+380•••••••21");
        var client = _factory.CreateClient();

        var status = await client.GetFromJsonAsync<TelegramLoginStatus>("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.LoggedIn, "+380•••••••21"), status);
    }

    [Fact]
    public async Task Get_ReturnsStepAsName()
    {
        _factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.Code);
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Contains("\"step\":\"Code\"", json);
    }

    [Fact]
    public async Task Post_PassesValueAndReturnsNextStep()
    {
        _factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.Code);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("+380987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.Code), await response.Content.ReadFromJsonAsync<TelegramLoginStatus>(TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "+380987654321" }, _factory.TelegramLogin.Submitted.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_EmptyValue_ReturnsBadRequestAndSubmitsNothing(string value)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue(value), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Value is required", await Detail(response));
        Assert.Empty(_factory.TelegramLogin.Submitted);
    }

    [Fact]
    public async Task Post_LoginRefusesValue_ReturnsBadRequestWithReason()
    {
        _factory.TelegramLogin.Exception = new TelegramLoginException("Wrong code");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("12345"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Wrong code", await Detail(response));
    }

    [Fact]
    public async Task Post_TelegramFails_ReturnsBadGatewayWithReason()
    {
        _factory.TelegramLogin.Exception = new InvalidOperationException("Connection failed");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("12345"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Connection failed", await Detail(response));
    }

    [Fact]
    public async Task Get_TelegramFails_ReturnsBadGatewayWithReason()
    {
        _factory.TelegramLogin.Exception = new InvalidOperationException("Connection failed");
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Connection failed", await Detail(response));
    }

    [Fact]
    public async Task Logout_LogsOutAndReturnsStep()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/telegram/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.Phone), await response.Content.ReadFromJsonAsync<TelegramLoginStatus>(TestContext.Current.CancellationToken));
        Assert.Equal(1, _factory.TelegramLogin.Logouts);
    }

    [Theory]
    [InlineData("/telegram/otp")]
    [InlineData("/telegram/password")]
    public async Task OldCodeEndpoints_AreGone(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(path, new { code = "12345" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        return problem?.Detail;
    }
}
