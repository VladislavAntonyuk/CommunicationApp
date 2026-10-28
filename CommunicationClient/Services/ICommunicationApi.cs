using Refit;
using Shared;

namespace CommunicationClient.Services;

public interface ICommunicationApi
{
    [Get("/providers")]
    Task<IApiResponse<List<ProviderStatus>>> GetProviders();

    [Put("/providers/{target}")]
    Task<IApiResponse> SetProvider(Target target, [Body] ProviderSwitch body);

    [Get("/providers/{target}/chats")]
    Task<IApiResponse<List<ChatStatus>>> GetChats(Target target);

    [Put("/providers/{target}/chats/{chatId}")]
    Task<IApiResponse> SetChat(Target target, string chatId, [Body] ProviderSwitch body);

    [Get("/telegram/login")]
    Task<IApiResponse<TelegramLoginStatus>> GetTelegramLogin();

    [Post("/telegram/login")]
    Task<IApiResponse<TelegramLoginStatus>> SendTelegramLogin([Body] TelegramLoginValue body);

    [Post("/telegram/logout")]
    Task<IApiResponse<TelegramLoginStatus>> TelegramLogout();
}
