using Refit;
using Shared.Api;

namespace CommunicationClient.Services;

public interface IAuthApi
{
    [Post("/auth/code")]
    Task<IApiResponse<LoginPhone>> RequestCode([Body] LoginPhone body);

    [Post("/auth/login")]
    Task<IApiResponse<AuthTokens>> Login([Body] LoginRequest body);

    [Post("/auth/refresh")]
    Task<IApiResponse<AuthTokens>> Refresh([Body] RefreshRequest body);
}
