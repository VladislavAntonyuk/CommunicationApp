using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Shared;
using TL;
using WTelegram;

namespace CommunicationApi.Services;

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
                if (u is UpdateNewMessage { message: TL.Message mb } && mb.From.ID != telegramClient.UserId)
                {
                    using var http = new HttpClient();

                    var payload = new Shared.Message($"Telegram|{mb.Peer.ID}|{mb.message}");

                    await http.PostAsJsonAsync($"{addresses.FirstOrDefault()}/notify/{Target.Telegram}", payload, cancellationToken: stoppingToken);
                }
            }
        };

        await Task.Delay(-1, stoppingToken);
    }
}