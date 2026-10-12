using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Shared;
using SlackNet;
using SlackNet.Events;

namespace CommunicationApi.Services.Slack;

public class SlackBackgroundService(IServiceProvider serviceProvider, IOptions<SlackSettings> settings) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var server = serviceProvider.GetRequiredService<IServer>();
        var feature = server.Features.Get<IServerAddressesFeature>();
        var addresses = feature.Addresses;
        var slackServices = new SlackServiceBuilder()
            .UseAppLevelToken(settings.Value.AppLevelToken)
            .RegisterEventHandler(ctx => new SlackMessageReceivedEventHandler(addresses.FirstOrDefault()));

        Console.WriteLine("Connecting...");

        var client = slackServices.GetSocketModeClient();
        await client.Connect(cancellationToken: stoppingToken);

        await Task.Delay(-1, stoppingToken);
    }
}

class SlackMessageReceivedEventHandler(string? address) : IEventHandler<MessageEvent>
{
    public async Task Handle(MessageEvent slackEvent)
    {
        if (string.IsNullOrEmpty(slackEvent.User))
            return;

        using var http = new HttpClient();

        var payload = new Shared.Message($"Slack|{slackEvent.User}|{slackEvent.Text}");

        await http.PostAsJsonAsync($"{address}/notify/{Target.Slack}", payload);
    }
}