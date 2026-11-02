using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

public sealed class MessageListener(Uri broker, string clientId, string topic, string failedTopic, TimeSpan retryDelay)
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan SessionExpiry = TimeSpan.FromDays(7);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);

    public async Task Run(Func<string, Task> handle, Action onConnected, Action<string> onFailed, CancellationToken token)
    {
        using var host = BuildHost(handle, async () =>
        {
            try
            {
                await KeepFailedMessages(token);
                onConnected();
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                onFailed(ex.Message);
            }
        });

        await host.StartAsync(token);
        try
        {
            var consumers = host.Services.GetRequiredService<IConsumerCollection>();
            using var timer = new PeriodicTimer(StatusInterval);
            while (await timer.WaitForNextTickAsync(token))
            {
                if (consumers.Any(consumer => consumer.StatusInfo.Status < ConsumerStatus.Connected))
                    onFailed("Not connected to the broker");
            }
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost BuildHost(Func<string, Task> handle, Func<Task> connected)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddLogging();
        builder.Services.AddSingleton(new ConnectedCallback(connected));
        builder.Services.AddSilverback()
            .WithConnectionToMessageBroker(options => options.AddMqtt().ConnectAfterStartup().RetryOnConnectionFailure(ReconnectDelay))
            .AddBrokerClientsConfigurator(_ => new Configurator(this))
            .AddSingletonBrokerClientCallback(services => services.GetRequiredService<ConnectedCallback>())
            .AddDelegateSubscriber<StringMessage>(message => handle(message.Content ?? ""));
        return builder.Build();
    }

    private void Configure(BrokerClientsConfigurationBuilder builder) => builder.AddMqttClients(clients => clients
        .ConnectViaTcp(broker.Host, broker.Port)
        .AddClient(client => client
            .WithClientId(clientId)
            .UseProtocolVersion(MqttProtocolVersion.V500)
            .RequestPersistentSession(SessionExpiry)
            .Consume(endpoint => endpoint
                .ConsumeFrom(topic)
                .ConsumeStrings()
                .WithAtLeastOnceQoS()
                .OnError(policy => policy
                    .Retry(MaxRetries, retry => retry.WithExponentialDelay(retryDelay, 2))
                    .ThenMoveTo(failedTopic)))
            .Produce<StringMessage>(endpoint => endpoint
                .ProduceTo(failedTopic)
                .ProduceStrings()
                .WithAtLeastOnceQoS())));

    private async Task KeepFailedMessages(CancellationToken token)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            e.AutoAcknowledge = false;
            return Task.CompletedTask;
        };

        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(broker)
            .WithClientId($"{clientId}-failed")
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanStart(false)
            .WithSessionExpiryInterval((uint)SessionExpiry.TotalSeconds)
            .Build(), token);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(failedTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), token);
        await client.DisconnectAsync(cancellationToken: token);
    }

    private sealed class Configurator(MessageListener listener) : IBrokerClientsConfigurator
    {
        public void Configure(BrokerClientsConfigurationBuilder builder) => listener.Configure(builder);
    }

    private sealed class ConnectedCallback(Func<Task> connected) : IMqttClientConnectedCallback
    {
        public Task OnClientConnectedAsync(MqttClientConfiguration configuration) => connected();
    }
}
