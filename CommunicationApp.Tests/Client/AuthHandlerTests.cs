using System.Net;
using CommunicationApi.Database;
using CommunicationApp.Tests.Helpers;
using CommunicationClient.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Shared.Api;

namespace CommunicationApp.Tests.Client;

public sealed class AuthHandlerTests : IAsyncDisposable
{
    private readonly ApiFactory _api = new();
    private readonly FakeTokenStorage _storage = new();
    private readonly ServiceProvider _services;

    public AuthHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITokenStorage>(_storage);
        services.AddSingleton<AuthSession>();
        services.AddTransient<AuthHandler>();
        services.AddRefitGeneratedClient<IAuthApi>()
            .ConfigureHttpClient(client => client.BaseAddress = _api.Server.BaseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => _api.Server.CreateHandler());
        services.AddRefitGeneratedClient<ICommunicationApi>()
            .ConfigureHttpClient(client => client.BaseAddress = _api.Server.BaseAddress)
            .AddHttpMessageHandler<AuthHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => _api.Server.CreateHandler());
        _services = services.BuildServiceProvider();
    }

    private ICommunicationApi Api => _services.GetRequiredService<ICommunicationApi>();

    private AuthSession Session => _services.GetRequiredService<AuthSession>();

    [Fact]
    public async Task LoggedIn_RequestSucceeds()
    {
        await Session.LogIn(await LogIn());

        var response = await Api.GetProviders();

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task NotLoggedIn_ReturnsUnauthorized()
    {
        var response = await Api.GetProviders();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RejectedAccessToken_RefreshesAndRetries()
    {
        var tokens = await LogIn();
        _storage.Saved = new SessionTokens("expired", tokens.RefreshToken);

        var response = await Api.GetProviders();

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEqual("expired", _storage.Saved!.AccessToken);
        Assert.NotEqual(tokens.RefreshToken, _storage.Saved.RefreshToken);
        Assert.Equal(1, _storage.Saves);
    }

    [Fact]
    public async Task RequestWithBody_IsSentAgainAfterRefresh()
    {
        var tokens = await LogIn();
        _storage.Saved = new SessionTokens("expired", tokens.RefreshToken);

        var response = await Api.SetProvider(Target.Telegram, new ProviderSwitch(false));

        Assert.True(response.IsSuccessStatusCode);
        using var scope = _api.Services.CreateScope();
        var setting = await scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>().ProviderSettings
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(setting.Enabled);
    }

    [Fact]
    public async Task ParallelRejectedRequests_RefreshOnce()
    {
        var tokens = await LogIn();
        _storage.Saved = new SessionTokens("expired", tokens.RefreshToken);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Api.GetProviders()));

        Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
        Assert.Equal(1, _storage.Saves);
    }

    [Fact]
    public async Task RejectedRefreshToken_LogsOut()
    {
        _storage.Saved = new SessionTokens("expired", "also-expired");
        var loggedOut = false;
        Session.LoggedOut += () => loggedOut = true;

        var response = await Api.GetProviders();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(_storage.Saved);
        Assert.True(loggedOut);
        Assert.False(await Session.IsLoggedIn());
    }

    [Fact]
    public async Task WrongCode_ShowsServerMessage()
    {
        var auth = _services.GetRequiredService<IAuthApi>();
        await auth.RequestCode(new LoginPhone(ApiFactory.ClientPhoneNumber));

        var response = await auth.Login(new LoginRequest(ApiFactory.ClientPhoneNumber, "wrong"));

        Assert.Equal("Wrong phone number or code", response.ErrorMessage);
    }

    private async Task<AuthTokens> LogIn()
    {
        var auth = _services.GetRequiredService<IAuthApi>();
        Assert.True((await auth.RequestCode(new LoginPhone(ApiFactory.ClientPhoneNumber))).IsSuccessStatusCode);
        var code = _api.Publisher.Sms.Last().Text[^6..];
        var response = await auth.Login(new LoginRequest(ApiFactory.ClientPhoneNumber, code));
        return response.Content!;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _api.DisposeAsync();
    }

    private sealed class FakeTokenStorage : ITokenStorage
    {
        public SessionTokens? Saved { get; set; }

        public int Saves { get; private set; }

        public Task<SessionTokens?> Load() => Task.FromResult(Saved);

        public Task Save(SessionTokens tokens)
        {
            Saved = tokens;
            Saves++;
            return Task.CompletedTask;
        }

        public void Clear() => Saved = null;
    }
}
