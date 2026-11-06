using Microsoft.Extensions.Options;
using Silverback.Messaging.Configuration;
using Shared;

namespace CommunicationApi.Services.Messaging;

public class BrokerClientsConfigurator(IConfiguration configuration, IOptions<MqttSettings> options) : IBrokerClientsConfigurator
{
    public const int MaxRetries = 3;

    public void Configure(BrokerClientsConfigurationBuilder builder)
    {
        var broker = new Uri(configuration.GetConnectionString("MessageBroker")!);

        builder.AddMqttClients(clients => clients
            .ConnectViaTcp(broker.Host, broker.Port)
            .AddClient(client => client
                .WithClientId(options.Value.ClientId)
                .RequestPersistentSession(options.Value.SessionExpiry)
                .Produce<OutgoingSms>(endpoint => endpoint
                    .ProduceTo(options.Value.Topic)
                    .SetResponseTopic(options.Value.AckTopic)
                    .SetCorrelationData(sms => sms?.Id)
                    .WithMessageExpiration(options.Value.MessageTtl)
                    .WithAtLeastOnceQoS())
                .Consume<SmsAck>(endpoint => endpoint
                    .ConsumeFrom(options.Value.AckTopic)
                    .WithAtLeastOnceQoS()
                    .OnError(policy => policy.Retry(2).ThenSkip()))
                .Consume<IncomingSms>(endpoint => endpoint
                    .ConsumeFrom(options.Value.IncomingTopic)
                    .WithAtLeastOnceQoS()
                    .OnError(policy => policy
                        .Retry(MaxRetries, retry => retry.WithExponentialDelay(options.Value.RetryDelay, 2))
                        .ThenSkip()))));
    }
}
