namespace CommunicationClient.Services;

public sealed class SecureTokenStorage(ISecureStorage storage) : ITokenStorage
{
    private const string AccessTokenKey = "accessToken";
    private const string RefreshTokenKey = "refreshToken";

    public async Task<SessionTokens?> Load()
    {
        try
        {
            var accessToken = await storage.GetAsync(AccessTokenKey);
            var refreshToken = await storage.GetAsync(RefreshTokenKey);
            return accessToken != null && refreshToken != null ? new SessionTokens(accessToken, refreshToken) : null;
        }
        catch (Exception)
        {
            storage.RemoveAll();
            return null;
        }
    }

    public async Task Save(SessionTokens tokens)
    {
        await storage.SetAsync(AccessTokenKey, tokens.AccessToken);
        await storage.SetAsync(RefreshTokenKey, tokens.RefreshToken);
    }

    public void Clear()
    {
        storage.Remove(AccessTokenKey);
        storage.Remove(RefreshTokenKey);
    }
}
