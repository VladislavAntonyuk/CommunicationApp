using Refit;
using Shared;

namespace CommunicationClient.Services;

public interface ICommunicationApi
{
    [Get("/providers")]
    Task<List<ProviderStatus>> GetProviders();

    [Put("/providers/{target}")]
    Task SetProvider(Target target, [Body] ProviderSwitch body);

    [Get("/providers/{target}/chats")]
    Task<List<ChatStatus>> GetChats(Target target);

    [Put("/providers/{target}/chats/{chatId}")]
    Task SetChat(Target target, string chatId, [Body] ProviderSwitch body);

    [Get("/telegram/login")]
    Task<TelegramLoginStatus?> GetTelegramLogin();

    [Post("/telegram/login")]
    Task<TelegramLoginStatus?> SendTelegramLogin([Body] TelegramLoginValue body);

    [Post("/telegram/logout")]
    Task<TelegramLoginStatus?> TelegramLogout();
}
