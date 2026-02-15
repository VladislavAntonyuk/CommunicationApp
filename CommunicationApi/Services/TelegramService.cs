using WTelegram;

namespace CommunicationApi.Services;

internal class TelegramService(IServiceProvider serviceProvider)
{
    public async Task Send(string recipient, string text)
    {
        var client = serviceProvider.GetRequiredService<Client>();
        await client.LoginUserIfNeeded();
        var chats = await client.Messages_GetAllChats();
        var chat = chats.chats[long.Parse(recipient)];
        await client.SendMessageAsync(chat, text);
    }
}