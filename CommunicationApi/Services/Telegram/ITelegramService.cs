namespace CommunicationApi.Services.Telegram;

public interface ITelegramService
{
    Task Send(string recipient, string text);
}
