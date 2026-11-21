namespace Shared.Api;

public record AuthTokens(string AccessToken, long ExpiresIn, string RefreshToken);
