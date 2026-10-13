using Shared;

namespace CommunicationApi.Services.Telegram;

public interface ITelegramService
{
    Task Send(string recipient, string text);
    Task<List<TelegramChat>> GetChats();
}

public record TelegramChat(string Id, string Title, ChatKind Kind);
