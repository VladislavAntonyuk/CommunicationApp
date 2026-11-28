using System.Net.Http.Headers;
using CommunicationApi.Database;
using CommunicationApi.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CommunicationApp.Tests.Helpers;

public static class TestTokens
{
    public static HttpClient Authorized(this HttpClient client, IServiceProvider services, string phoneNumber = ApiFactory.ClientPhoneNumber)
    {
        var options = services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            UserPrincipal.Create(SaveUser(services, phoneNumber)),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) },
            BearerTokenDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerTokenProtector.Protect(ticket));
        return client;
    }

    public static User SaveUser(IServiceProvider services, string phoneNumber = ApiFactory.ClientPhoneNumber)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
        var user = dbContext.Users.FirstOrDefault(x => x.PhoneNumber == phoneNumber);
        if (user != null)
            return user;

        user = new User { PhoneNumber = phoneNumber, CreatedAt = DateTime.UtcNow };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }
}
