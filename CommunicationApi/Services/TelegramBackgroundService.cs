using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Shared;
using TL;
using WTelegram;

namespace CommunicationApi.Services;

public class TelegramBackgroundService(IConfiguration configuration, IServiceProvider serviceProvider):BackgroundService
{
    public static string? _password = null;
    public static string? _otp = null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var telegram = configuration.GetSection("Telegram").Get<TelegramSettings>();

        string ConfigProvider(string what)
        {
            switch (what)
            {
                case "api_id":
                    return telegram.AppId;
                case "api_hash":
                    return telegram.AppHash;
                case "phone_number":
                    return telegram.Phone;
                case "session_pathname":
                    return Path.Combine(AppContext.BaseDirectory, "telegram.session");
                case "verification_code":
                    while (_otp == null)
                        Thread.Sleep(1000);

                    return _otp;
                case "password":
                    while (_password == null)
                        Thread.Sleep(1000);

                    return _password;
                default:
                    return null;
            }
        }

        await using var client = new Client(ConfigProvider);

        var me = await client.LoginUserIfNeeded();
        Console.WriteLine($"Logged in as {me.username ?? me.first_name}");
        var server = serviceProvider.GetRequiredService<IServer>();
        var feature = server.Features.Get<IServerAddressesFeature>();
        var addresses = feature.Addresses;
        client.OnUpdates += async (updates) =>
        {
            foreach (var u in updates.UpdateList)
            {
                if (u is UpdateNewMessage { message: TL.Message mb } && mb.From.ID != me.ID)
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