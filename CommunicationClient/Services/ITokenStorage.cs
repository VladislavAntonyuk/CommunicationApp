namespace CommunicationClient.Services;

public record SessionTokens(string AccessToken, string RefreshToken);

public interface ITokenStorage
{
    Task<SessionTokens?> Load();

    Task Save(SessionTokens tokens);

    void Clear();
}
