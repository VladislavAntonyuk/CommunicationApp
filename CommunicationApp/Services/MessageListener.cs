using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Shared;
using Silverback.Configuration;
using Silverback.Messaging.Broker;
using Silverback.Messaging.Broker.Callbacks;
using Silverback.Messaging.Configuration;
using Silverback.Messaging.Configuration.Mqtt;

namespace CommunicationApp.Services;

public sealed class MessageListener(
    IBrokerClientsConnector connector,
    IConsumerCollection consumers,
    IMessageHandler handler) : IMqttClientConnectedCallback
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);

    private bool _initialized;

    public async Task Run(CancellationToken token)
    {
        if (!_initialized)
        {
            await connector.InitializeAsync();
            _initialized = true;
        }

        await connector.ConnectAsync(token);
        try
        {
            using var timer = new PeriodicTimer(StatusInterval);
            while (await timer.WaitForNextTickAsync(token))
            {
                if (consumers.Any(consumer => consumer.StatusInfo.Status < ConsumerStatus.Connected))
                    handler.Failed("Not connected to the broker");
            }
        }
        finally
        {
            await connector.DisconnectAsync();
        }
    }

    public Task OnClientConnectedAsync(MqttClientConfiguration configuration)
    {
        handler.Connected();
        return Task.CompletedTask;
    }
}

public static class MessageListenerServiceCollectionExtensions
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddMessageListener(this IServiceCollection services)
    {
        services.TryAddSingleton<IHostApplicationLifetime, ApplicationLifetime>();
        services.AddSingleton<MessageListener>();
        services.AddSilverback()
            .WithConnectionToMessageBroker(options => options.AddMqtt().ManuallyConnect().RetryOnConnectionFailure(ReconnectDelay))
            .AddBrokerClientsConfigurator<BrokerClientsConfigurator>()
            .AddSingletonBrokerClientCallback(provider => provider.GetRequiredService<MessageListener>())
            .AddDelegateSubscriber((OutgoingSms sms, IMessageHandler handler) => handler.Handle(sms.Text));
        return services;
    }
}
