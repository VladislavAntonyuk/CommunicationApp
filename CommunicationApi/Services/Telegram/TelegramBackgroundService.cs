using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Shared;
using TL;
using WTelegram;

namespace CommunicationApi.Services.Telegram;

public class TelegramBackgroundService(IServiceProvider serviceProvider):BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var server = serviceProvider.GetRequiredService<IServer>();
        var feature = server.Features.Get<IServerAddressesFeature>();
        var addresses = feature.Addresses;
        var telegramClient = serviceProvider.GetRequiredService<Client>();
        await telegramClient.LoginUserIfNeeded();
        telegramClient.OnUpdates += async (updates) =>
        {
            foreach (var u in updates.UpdateList)
            {
                if (u is UpdateNewMessage { message: TL.Message mb } && !mb.flags.HasFlag(TL.Message.Flags.out_))
                {
                    using var http = new HttpClient();

                    var kind = mb.peer_id switch
                    {
                        PeerUser => ChatKind.Private,
                        PeerChannel peer when updates.Chats.TryGetValue(peer.channel_id, out var chat) && chat.IsChannel => ChatKind.Channel,
                        _ => ChatKind.Group
                    };

                    var payload = new Shared.Message($"Telegram|{mb.peer_id.ID}|{mb.message}", mb.peer_id.ID.ToString(), kind);

                    await http.PostAsJsonAsync($"{addresses.FirstOrDefault()}/notify/{Target.Telegram}", payload, cancellationToken: stoppingToken);
                }
            }
        };

        await Task.Delay(-1, stoppingToken);
    }
}