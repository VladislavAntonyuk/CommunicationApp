using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shared.Api;

namespace CommunicationClient.Services;

public sealed class AuthSession(ITokenStorage storage, IServiceProvider services)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SessionTokens? _tokens;
    private bool _loaded;

    public event Action? LoggedOut;

    public async Task<bool> IsLoggedIn() => await AccessToken() != null;

    public async Task<string?> AccessToken()
    {
        await _lock.WaitAsync();
        try
        {
            return (await Current())?.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task LogIn(AuthTokens tokens)
    {
        await _lock.WaitAsync();
        try
        {
            await Store(tokens);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task LogOut()
    {
        await _lock.WaitAsync();
        try
        {
            Forget();
        }
        finally
        {
            _lock.Release();
        }

        LoggedOut?.Invoke();
    }

    public async Task<string?> Refresh(string rejectedAccessToken)
    {
        var loggedOut = false;
        await _lock.WaitAsync();
        try
        {
            var tokens = await Current();
            if (tokens == null)
                return null;

            if (tokens.AccessToken != rejectedAccessToken)
                return tokens.AccessToken;

            var response = await services.GetRequiredService<IAuthApi>().Refresh(new RefreshRequest(tokens.RefreshToken));
            if (response.IsSuccessfulWithContent)
                return (await Store(response.Content)).AccessToken;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Forget();
                loggedOut = true;
            }

            return null;
        }
        finally
        {
            _lock.Release();
            if (loggedOut)
                LoggedOut?.Invoke();
        }
    }

    private async Task<SessionTokens?> Current()
    {
        if (!_loaded)
        {
            _tokens = await storage.Load();
            _loaded = true;
        }

        return _tokens;
    }

    private async Task<SessionTokens> Store(AuthTokens tokens)
    {
        var session = new SessionTokens(tokens.AccessToken, tokens.RefreshToken);
        await storage.Save(session);
        _tokens = session;
        _loaded = true;
        return session;
    }

    private void Forget()
    {
        storage.Clear();
        _tokens = null;
        _loaded = true;
    }
}
