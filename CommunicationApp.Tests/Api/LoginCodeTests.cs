using System.Net;
using System.Net.Http.Json;
using CommunicationApi.Database;
using CommunicationApp.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;

namespace CommunicationApp.Tests.Api;

public sealed class LoginCodeTests : ApiTest
{
    private const string PhoneNumber = "+380501234567";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidNumber_SendsCodeToNormalizedNumber()
    {
        var response = await RequestCode("050 123 45 67");

        response.EnsureSuccessStatusCode();
        Assert.Equal(new LoginPhone(PhoneNumber), await response.Content.ReadFromJsonAsync<LoginPhone>(Token));
        var sms = Assert.Single(Factory.Publisher.Sms);
        Assert.Equal(PhoneNumber, sms.To);
        Assert.Matches(@"^Your login code: \d{6}$", sms.Text);
    }

    [Fact]
    public async Task StoresHashNotCode()
    {
        await RequestCode(PhoneNumber);

        var code = Factory.Publisher.Sms.Single().Text[^6..];
        var saved = await InDb(db => db.LoginCodes.SingleAsync(Token));
        Assert.DoesNotContain(code, saved.CodeHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<LoginCode>().VerifyHashedPassword(saved, saved.CodeHash, code));
    }

    [Fact]
    public async Task SavesMessageWaitingForReply()
    {
        await RequestCode(PhoneNumber);

        var message = await InDb(db => db.OutgoingMessages.SingleAsync(Token));
        Assert.Equal(Factory.Publisher.Sms.Single().Id, message.Id);
        Assert.Null(message.Sent);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("+48512345678")]
    public async Task InvalidOrNotAllowedNumber_ReturnsBadRequest(string phoneNumber)
    {
        var response = await RequestCode(phoneNumber);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Factory.Publisher.Sms);
    }

    [Fact]
    public async Task SecondRequestWithinResendDelay_ReturnsTooManyRequests()
    {
        await RequestCode(PhoneNumber);

        var response = await RequestCode("0501234567");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.InRange(response.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 55, 60);
        Assert.Single(Factory.Publisher.Sms);
    }

    [Fact]
    public async Task AfterResendDelay_SendsNewCode()
    {
        await AddCodes(DateTime.UtcNow.AddMinutes(-2));

        var response = await RequestCode(PhoneNumber);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FiveCodesInLastHour_ReturnsTooManyRequests()
    {
        var now = DateTime.UtcNow;
        await AddCodes(now.AddMinutes(-50), now.AddMinutes(-40), now.AddMinutes(-30), now.AddMinutes(-20), now.AddMinutes(-10));

        var response = await RequestCode(PhoneNumber);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.InRange(response.Headers.RetryAfter!.Delta!.Value.TotalMinutes, 9, 10);
        Assert.Empty(Factory.Publisher.Sms);
    }

    [Fact]
    public async Task CodesOlderThanHour_AreNotCounted()
    {
        var now = DateTime.UtcNow;
        await AddCodes(now.AddMinutes(-90), now.AddMinutes(-80), now.AddMinutes(-70), now.AddMinutes(-65), now.AddMinutes(-61));

        var response = await RequestCode(PhoneNumber);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AnyNumber_GetsCode()
    {
        var response = await RequestCode(ApiFactory.OtherPhoneNumber);

        response.EnsureSuccessStatusCode();
        Assert.Equal(ApiFactory.OtherPhoneNumber, Factory.Publisher.Sms.Single().To);
    }

    private Task<HttpResponseMessage> RequestCode(string phoneNumber) =>
        Client.PostAsJsonAsync("/auth/code", new LoginPhone(phoneNumber), Token);

    private Task AddCodes(params DateTime[] createdAt) => InDb(async db =>
    {
        db.LoginCodes.AddRange(createdAt.Select(time => new LoginCode { PhoneNumber = PhoneNumber, CodeHash = "x", CreatedAt = time }));
        return await db.SaveChangesAsync(Token);
    });

    private async Task<T> InDb<T>(Func<CommunicationApiDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>());
    }
}
