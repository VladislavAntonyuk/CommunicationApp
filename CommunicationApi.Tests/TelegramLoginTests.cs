using System.Net;
using System.Net.Http.Json;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class TelegramLoginTests : ApiTest
{
    [Fact]
    public async Task Get_ReturnsCurrentStep()
    {
        Factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.LoggedIn, "+380•••••••21");

        var status = await Client.GetFromJsonAsync<TelegramLoginStatus>("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.LoggedIn, "+380•••••••21"), status);
    }

    [Fact]
    public async Task Get_ReturnsStepAsName()
    {
        Factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.Code);

        var json = await Client.GetStringAsync("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Contains("\"step\":\"Code\"", json);
    }

    [Fact]
    public async Task Post_PassesValueAndReturnsNextStep()
    {
        Factory.TelegramLogin.Status = new TelegramLoginStatus(TelegramLoginStep.Code);

        var response = await Client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("+380987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.Code), await response.Content.ReadFromJsonAsync<TelegramLoginStatus>(TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "+380987654321" }, Factory.TelegramLogin.Submitted.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_EmptyValue_ReturnsBadRequestAndSubmitsNothing(string value)
    {
        var response = await Client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue(value), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Value is required", await Detail(response));
        Assert.Empty(Factory.TelegramLogin.Submitted);
    }

    [Fact]
    public async Task Post_LoginRefusesValue_ReturnsBadRequestWithReason()
    {
        Factory.TelegramLogin.Exception = new TelegramLoginException("Wrong code");

        var response = await Client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("12345"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Wrong code", await Detail(response));
    }

    [Fact]
    public async Task Post_TelegramFails_ReturnsBadGatewayWithReason()
    {
        Factory.TelegramLogin.Exception = new InvalidOperationException("Connection failed");

        var response = await Client.PostAsJsonAsync("/telegram/login", new TelegramLoginValue("12345"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Connection failed", await Detail(response));
    }

    [Fact]
    public async Task Get_TelegramFails_ReturnsBadGatewayWithReason()
    {
        Factory.TelegramLogin.Exception = new InvalidOperationException("Connection failed");

        var response = await Client.GetAsync("/telegram/login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Connection failed", await Detail(response));
    }

    [Fact]
    public async Task Logout_LogsOutAndReturnsStep()
    {
        var response = await Client.PostAsync("/telegram/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TelegramLoginStatus(TelegramLoginStep.Phone), await response.Content.ReadFromJsonAsync<TelegramLoginStatus>(TestContext.Current.CancellationToken));
        Assert.Equal(1, Factory.TelegramLogin.Logouts);
    }

    [Theory]
    [InlineData("/telegram/otp")]
    [InlineData("/telegram/password")]
    public async Task OldCodeEndpoints_AreGone(string path)
    {
        var response = await Client.PostAsJsonAsync(path, new { code = "12345" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        return problem?.Detail;
    }
}
