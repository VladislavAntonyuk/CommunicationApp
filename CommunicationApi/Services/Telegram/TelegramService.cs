using TL;
using WTelegram;

namespace CommunicationApi.Services.Telegram;

internal class TelegramService(IServiceProvider serviceProvider) : ITelegramService
{
    public async Task Send(string recipient, string text)
    {
        var client = serviceProvider.GetRequiredService<Client>();
        await client.LoginUserIfNeeded();
        var id = long.Parse(recipient);
        var dialogs = await client.Messages_GetAllDialogs();
        InputPeer peer = dialogs.users.TryGetValue(id, out var user) ? user
            : dialogs.chats.TryGetValue(id, out var chat) ? chat
            : throw new ArgumentException($"Telegram chat {recipient} not found");
        await client.SendMessageAsync(peer, text);
    }
}