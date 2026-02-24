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
            .UseApiToken(settings.Value.ApiToken)
            .UseAppLevelToken(settings.Value.AppLevelToken)
            .RegisterEventHandler(ctx => new SlackMessageReceivedEventHandler(ctx.ServiceProvider.GetApiClient(), addresses.FirstOrDefault()));

        Console.WriteLine("Connecting...");

        var client = slackServices.GetSocketModeClient();
        await client.Connect(cancellationToken: stoppingToken);

        await Task.Delay(-1, stoppingToken);
    }
}

class SlackMessageReceivedEventHandler(ISlackApiClient slack, string? address) : IEventHandler<MessageEvent>
{
    public async Task Handle(MessageEvent slackEvent)
    {
        using var http = new HttpClient();

        var payload = new Shared.Message($"Slack|{(await slack.Users.Info(slackEvent.User)).Id}|{slackEvent.Text}");

        await http.PostAsJsonAsync($"{address}/notify/{Target.Telegram}", payload);
    }
}