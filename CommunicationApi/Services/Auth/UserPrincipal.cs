using System.Security.Claims;
using CommunicationApi.Database;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace CommunicationApi.Services.Auth;

public static class UserPrincipal
{
    public static ClaimsPrincipal Create(User user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber)
            ],
            BearerTokenDefaults.AuthenticationScheme));

    public static int? UserId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
