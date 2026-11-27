using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunicationApp.Tests.Helpers;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class AuthorizationTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/providers")]
    [InlineData("PUT", "/providers/Telegram")]
    [InlineData("GET", "/providers/Telegram/chats")]
    [InlineData("PUT", "/providers/Telegram/chats/1")]
    [InlineData("GET", "/telegram/login")]
    [InlineData("POST", "/telegram/login")]
    [InlineData("POST", "/telegram/logout")]
    public async Task WithoutToken_ReturnsUnauthorized(string method, string path)
    {
        var response = await AnonymousClient.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithGarbageToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/providers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await AnonymousClient.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithTokenFromLogin_ReturnsOk()
    {
        (await AnonymousClient.PostAsJsonAsync("/auth/code", new LoginPhone(ApiFactory.ClientPhoneNumber), Token)).EnsureSuccessStatusCode();
        var code = Factory.Publisher.Sms.Single().Text[^6..];
        var login = await AnonymousClient.PostAsJsonAsync("/auth/login", new LoginRequest(ApiFactory.ClientPhoneNumber, code), Token);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>(Token);
        var request = new HttpRequestMessage(HttpMethod.Get, "/providers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

        var response = await AnonymousClient.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Root_WorksWithoutToken()
    {
        var response = await AnonymousClient.GetAsync("/", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Notify_IsGone()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/notify/Telegram", new Message("Telegram|1|hello"), Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
