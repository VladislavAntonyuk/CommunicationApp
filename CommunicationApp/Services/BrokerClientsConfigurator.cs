using Microsoft.Extensions.Options;
using MQTTnet.Formatter;
using Shared;
using Silverback.Messaging.Configuration;

namespace CommunicationApp.Services;

public class BrokerClientsConfigurator(IOptions<MqttSettings> options) : IBrokerClientsConfigurator
{
    private const int MaxRetries = 3;

    public void Configure(BrokerClientsConfigurationBuilder builder)
    {
        var settings = options.Value;

        builder.AddMqttClients(clients => clients
            .ConnectViaTcp(settings.Broker.Host, settings.Broker.Port)
            .AddClient(client => client
                .WithClientId(settings.ClientId)
                .UseProtocolVersion(MqttProtocolVersion.V500)
                .RequestPersistentSession(settings.SessionExpiry)
                .Consume(endpoint => endpoint
                    .ConsumeFrom(settings.Topic)
                    .DeserializeJson(serializer => serializer.UseModel<OutgoingSms>().IgnoreMessageTypeHeader())
                    .WithAtLeastOnceQoS()
                    .OnError(policy => policy
                        .Retry(MaxRetries, retry => retry.WithExponentialDelay(settings.RetryDelay, 2))
                        .ThenSkip()))));
    }
}
