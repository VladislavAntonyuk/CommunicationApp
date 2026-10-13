using Shared;
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

    public async Task<List<TelegramChat>> GetChats()
    {
        var client = serviceProvider.GetRequiredService<Client>();
        // LoginUserIfNeeded would wait for a login code here, and the request would never finish.
        if (client.User == null)
            throw new InvalidOperationException("Telegram is not logged in");

        var dialogs = await client.Messages_GetAllDialogs();
        var chats = new List<TelegramChat>();
        foreach (var dialog in dialogs.dialogs)
        {
            switch (dialogs.UserOrChat(dialog))
            {
                case User { IsActive: true } user when !user.flags.HasFlag(User.Flags.self):
                    var name = $"{user.first_name} {user.last_name}".Trim();
                    chats.Add(new TelegramChat(user.ID.ToString(), name.Length > 0 ? name : user.MainUsername ?? user.ID.ToString(), ChatKind.Private));
                    break;
                case ChatBase { IsActive: true } chat:
                    chats.Add(new TelegramChat(chat.ID.ToString(), chat.Title, chat.IsChannel ? ChatKind.Channel : ChatKind.Group));
                    break;
            }
        }

        return chats;
    }
}