using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Silverback.Configuration;
using Silverback.Messaging.Broker;
using Silverback.Messaging.Broker.Callbacks;
using Silverback.Messaging.Configuration;
using Silverback.Messaging.Configuration.Mqtt;
using Silverback.Messaging.Messages;

namespace CommunicationApp.Services;

public sealed class MessageListener(
    IBrokerClientsConnector connector,
    IConsumerCollection consumers,
    IOptions<MqttSettings> options,
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

    public async Task OnClientConnectedAsync(MqttClientConfiguration configuration)
    {
        try
        {
            await KeepFailedMessages();
            handler.Connected();
        }
        catch (Exception ex)
        {
            handler.Failed(ex.Message);
        }
    }

    private async Task KeepFailedMessages()
    {
        var settings = options.Value;
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            e.AutoAcknowledge = false;
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(settings.Broker)
            .WithClientId($"{settings.ClientId}-failed")
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanStart(false)
            .WithSessionExpiryInterval((uint)settings.SessionExpiry.TotalSeconds)
            .Build());
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(settings.FailedTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build());
        await client.DisconnectAsync();
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
            .AddDelegateSubscriber((StringMessage message, IMessageHandler handler) => handler.Handle(message.Content ?? ""));
        return services;
    }
}
