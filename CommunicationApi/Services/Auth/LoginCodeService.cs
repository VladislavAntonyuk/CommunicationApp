using System.Security.Cryptography;
using CommunicationApi.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;
using Silverback.Messaging.Publishing;

namespace CommunicationApi.Services.Auth;

public abstract record SendCodeResult
{
    public sealed record Sent(string PhoneNumber) : SendCodeResult;

    public sealed record InvalidNumber : SendCodeResult;

    public sealed record TooSoon(TimeSpan RetryAfter) : SendCodeResult;
}

public abstract record CheckCodeResult
{
    public sealed record Valid(User User) : CheckCodeResult;

    public sealed record Invalid(string Reason) : CheckCodeResult;

    public sealed record TooManyAttempts : CheckCodeResult;
}

public sealed class LoginCodeService(CommunicationApiDbContext dbContext, IPublisher publisher, IOptions<AuthSettings> options)
{
    private static readonly TimeSpan LimitWindow = TimeSpan.FromHours(1);
    private static readonly PasswordHasher<LoginCode> Hasher = new();

    public async Task<SendCodeResult> SendCode(string? input)
    {
        var settings = options.Value;
        var phoneNumber = PhoneNumberNormalizer.ToE164(input, settings.DefaultRegion, settings.AllowedRegions);
        if (phoneNumber == null)
            return new SendCodeResult.InvalidNumber();

        var now = DateTime.UtcNow;
        var since = now - LimitWindow;
        var recent = await dbContext.LoginCodes
            .Where(x => x.PhoneNumber == phoneNumber && x.CreatedAt > since)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.CreatedAt)
            .ToListAsync();

        if (recent.Count > 0 && now - recent[^1] < settings.ResendDelay)
            return new SendCodeResult.TooSoon(recent[^1] + settings.ResendDelay - now);

        if (recent.Count >= settings.MaxCodesPerHour)
            return new SendCodeResult.TooSoon(recent[^settings.MaxCodesPerHour] + LimitWindow - now);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var loginCode = new LoginCode { PhoneNumber = phoneNumber, CodeHash = "", CreatedAt = now };
        loginCode.CodeHash = Hasher.HashPassword(loginCode, code);
        dbContext.LoginCodes.Add(loginCode);

        var id = Guid.NewGuid().ToString("N");
        dbContext.OutgoingMessages.Add(new OutgoingMessage { Id = id, CreatedAt = now });
        await dbContext.SaveChangesAsync();

        await publisher.PublishAsync(new OutgoingSms(phoneNumber, $"Your login code: {code}", id));
        return new SendCodeResult.Sent(phoneNumber);
    }

    public async Task<CheckCodeResult> CheckCode(string? input, string? code)
    {
        var settings = options.Value;
        var phoneNumber = PhoneNumberNormalizer.ToE164(input, settings.DefaultRegion, settings.AllowedRegions);
        if (phoneNumber == null || string.IsNullOrWhiteSpace(code))
            return new CheckCodeResult.Invalid("Wrong phone number or code");

        var now = DateTime.UtcNow;
        var since = now - LimitWindow;
        var recent = await dbContext.LoginCodes
            .AsTracking()
            .Where(x => x.PhoneNumber == phoneNumber && x.CreatedAt > since)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        if (recent.Sum(x => x.FailedAttempts) >= settings.MaxFailedAttemptsPerHour)
            return new CheckCodeResult.TooManyAttempts();

        var latest = recent.LastOrDefault();
        if (latest == null || now - latest.CreatedAt > settings.CodeLifetime)
            return new CheckCodeResult.Invalid("Code expired, request a new one");

        if (Hasher.VerifyHashedPassword(latest, latest.CodeHash, code.Trim()) == PasswordVerificationResult.Failed)
        {
            latest.FailedAttempts++;
            await dbContext.SaveChangesAsync();
            return new CheckCodeResult.Invalid("Wrong phone number or code");
        }

        dbContext.LoginCodes.Remove(latest);
        var user = await dbContext.Users.AsTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber);
        if (user == null)
        {
            user = new User { PhoneNumber = phoneNumber, CreatedAt = now };
            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync();
        return new CheckCodeResult.Valid(user);
    }
}
