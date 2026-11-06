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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:MessageBroker", connectionString);
        builder.UseSetting("Mqtt:ClientId", $"communication-api-{_id}");
        builder.UseSetting("Mqtt:Topic", Topic);
        builder.UseSetting("Mqtt:AckTopic", AckTopic);
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
