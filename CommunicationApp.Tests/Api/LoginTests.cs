using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CommunicationApi.Database;
using CommunicationApp.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class LoginTests : ApiTest
{
    private const string PhoneNumber = "+380501234567";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RightCode_ReturnsTokens()
    {
        var code = await RequestCode();

        var response = await Login("050 123 45 67", code);

        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.NotEmpty(tokens.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(tokens.GetProperty("refreshToken").GetString()!);
        Assert.Equal(3600, tokens.GetProperty("expiresIn").GetInt64());
    }

    [Fact]
    public async Task RightCode_CreatesUserOnce()
    {
        await Login(PhoneNumber, await RequestCode());
        await MoveCodesBack(TimeSpan.FromMinutes(2));
        await Login(PhoneNumber, await RequestCode());

        var users = await InDb(db => db.Users.ToListAsync(Token));
        Assert.Equal(PhoneNumber, Assert.Single(users).PhoneNumber);
    }

    [Fact]
    public async Task UsedCode_DoesNotWorkAgain()
    {
        var code = await RequestCode();
        (await Login(PhoneNumber, code)).EnsureSuccessStatusCode();

        var response = await Login(PhoneNumber, code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WrongCode_ReturnsBadRequestAndCountsAttempt()
    {
        var code = await RequestCode();

        var response = await Login(PhoneNumber, WrongCode(code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await InDb(db => db.LoginCodes.SingleAsync(Token))).FailedAttempts);
        Assert.Empty(await InDb(db => db.Users.ToListAsync(Token)));
    }

    [Fact]
    public async Task FiveWrongCodes_BlocksEvenRightCode()
    {
        var code = await RequestCode();
        for (var i = 0; i < 5; i++)
            await Login(PhoneNumber, WrongCode(code));

        var response = await Login(PhoneNumber, code);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ParallelWrongCodes_AreAllCounted()
    {
        var code = await RequestCode();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Login(PhoneNumber, WrongCode(code))));

        Assert.Equal(5, (await InDb(db => db.LoginCodes.SingleAsync(Token))).FailedAttempts);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(PhoneNumber, code)).StatusCode);
    }

    [Fact]
    public async Task NewCode_AllowsNewAttempts()
    {
        var first = await RequestCode();
        for (var i = 0; i < 5; i++)
            await Login(PhoneNumber, WrongCode(first));
        await MoveCodesBack(TimeSpan.FromMinutes(2));

        var response = await Login(PhoneNumber, await RequestCode());

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OlderCode_DoesNotWorkAfterNewOne()
    {
        var first = await RequestCode();
        await MoveCodesBack(TimeSpan.FromMinutes(2));
        await RequestCode();

        var response = await Login(PhoneNumber, first);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredCode_ReturnsBadRequest()
    {
        var code = await RequestCode();
        await MoveCodesBack(TimeSpan.FromMinutes(11));

        var response = await Login(PhoneNumber, code);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NoCodeRequested_ReturnsBadRequest()
    {
        var response = await Login(PhoneNumber, "123456");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens()
    {
        var tokens = await LoggedIn();

        var response = await Refresh(tokens.GetProperty("refreshToken").GetString()!);

        response.EnsureSuccessStatusCode();
        var refreshed = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.NotEmpty(refreshed.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(refreshed.GetProperty("refreshToken").GetString()!);
    }

    [Fact]
    public async Task Refresh_WithAccessToken_ReturnsUnauthorized()
    {
        var tokens = await LoggedIn();

        var response = await Refresh(tokens.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithGarbage_ReturnsUnauthorized()
    {
        var response = await Refresh("not-a-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ForDeletedUser_ReturnsUnauthorized()
    {
        var tokens = await LoggedIn();
        await InDb(db => db.Users.ExecuteDeleteAsync(Token));

        var response = await Refresh(tokens.GetProperty("refreshToken").GetString()!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<JsonElement> LoggedIn()
    {
        var response = await Login(PhoneNumber, await RequestCode());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Token);
    }

    private Task<HttpResponseMessage> Refresh(string refreshToken) =>
        Client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken), Token);

    private async Task<string> RequestCode()
    {
        var before = Factory.Publisher.Sms.Count;
        (await Client.PostAsJsonAsync("/auth/code", new LoginPhone(PhoneNumber), Token)).EnsureSuccessStatusCode();
        return Factory.Publisher.Sms[before].Text[^6..];
    }

    private Task<HttpResponseMessage> Login(string phoneNumber, string code) =>
        Client.PostAsJsonAsync("/auth/login", new LoginRequest(phoneNumber, code), Token);

    private static string WrongCode(string code) => code == "000000" ? "111111" : "000000";

    private Task MoveCodesBack(TimeSpan by) => InDb(async db =>
    {
        foreach (var code in await db.LoginCodes.AsTracking().ToListAsync(Token))
            code.CreatedAt -= by;
        return await db.SaveChangesAsync(Token);
    });

    private async Task<T> InDb<T>(Func<CommunicationApiDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>());
    }
}
