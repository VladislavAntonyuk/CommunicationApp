using Shared;

namespace CommunicationApi.Services.Telegram;

public interface ITelegramLogin
{
    Task<TelegramLoginStatus> GetStatus();

    Task<TelegramLoginStatus> Submit(string value);

    Task<TelegramLoginStatus> Logout();
}

public class TelegramLoginException(string message) : Exception(message);
