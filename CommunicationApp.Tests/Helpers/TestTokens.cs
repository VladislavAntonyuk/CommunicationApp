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
    public static HttpClient Authorized(this HttpClient client, IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(BearerTokenDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            UserPrincipal.Create(new User { Id = 1, PhoneNumber = ApiFactory.ClientPhoneNumber }),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) },
            BearerTokenDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerTokenProtector.Protect(ticket));
        return client;
    }
}
