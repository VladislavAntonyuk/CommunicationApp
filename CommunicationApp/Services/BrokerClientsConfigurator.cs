using Microsoft.Extensions.Options;
using MQTTnet.Formatter;
using Shared;
using Silverback.Messaging.Configuration;

namespace CommunicationApp.Services;

public class BrokerClientsConfigurator(IOptions<MqttSettings> options) : IBrokerClientsConfigurator
{
    public const int MaxRetries = 3;

    public void Configure(BrokerClientsConfigurationBuilder builder)
    {
        var settings = options.Value;

        builder.AddMqttClients(clients => clients
            .ConnectTo(settings.Broker)
            .AddClient(client => client
                .WithClientId(settings.ClientId)
                .UseProtocolVersion(MqttProtocolVersion.V500)
                .RequestPersistentSession(settings.SessionExpiry)
                .Consume<OutgoingSms>(endpoint => endpoint
                    .ConsumeFrom(settings.Topic)
                    .WithAtLeastOnceQoS()
                    .OnError(policy => policy
                        .Retry(MaxRetries, retry => retry.WithExponentialDelay(settings.RetryDelay, 2))
                        .ThenSkip()))
                .Produce<SmsAck>(endpoint => endpoint
                    .ProduceToDynamicTopic()
                    .WithAtLeastOnceQoS())
                .Produce<IncomingSms>(endpoint => endpoint
                    .ProduceTo(settings.IncomingTopic)
                    .SetCorrelationData(sms => sms?.Id)
                    .WithAtLeastOnceQoS())));
    }
}
