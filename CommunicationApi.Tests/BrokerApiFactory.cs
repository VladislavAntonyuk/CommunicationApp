using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunicationApi.Tests;

public sealed class BrokerApiFactory(string connectionString) : ApiFactory
{
    private readonly string _id = Guid.NewGuid().ToString("N");

    public string Topic => $"messages/{_id}";

    public string AckTopic => $"{Topic}/ack";

    public string IncomingTopic => $"{Topic}/incoming";

    public string ClientId => $"communication-api-{_id}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:MessageBroker", connectionString);
        builder.UseSetting("Mqtt:ClientId", ClientId);
        builder.UseSetting("Mqtt:Topic", Topic);
        builder.UseSetting("Mqtt:AckTopic", AckTopic);
        builder.UseSetting("Mqtt:IncomingTopic", IncomingTopic);
        builder.UseSetting("Mqtt:RetryDelay", "00:00:00.010");
        base.ConfigureWebHost(builder);
    }

    protected override void ConfigureMessaging(IServiceCollection services)
    {
        RemoveHostedService<TelegramBackgroundService>(services);
        RemoveHostedService<SlackBackgroundService>(services);
    }

    private static void RemoveHostedService<T>(IServiceCollection services)
    {
        services.Remove(services.Single(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(T)));
    }
}
