using Microsoft.Extensions.Options;
using Silverback.Messaging.Configuration;
using Shared;

namespace CommunicationApi.Services.Messaging;

public class BrokerClientsConfigurator(IConfiguration configuration, IOptions<MqttSettings> options) : IBrokerClientsConfigurator
{
    public void Configure(BrokerClientsConfigurationBuilder builder)
    {
        var broker = new Uri(configuration.GetConnectionString("MessageBroker")!);

        builder.AddMqttClients(clients => clients
            .ConnectViaTcp(broker.Host, broker.Port)
            .AddClient(client => client
                .WithClientId(options.Value.ClientId)
                .Produce<OutgoingSms>(endpoint => endpoint
                    .ProduceTo(options.Value.Topic)
                    .SetCorrelationData(sms => sms?.Id)
                    .WithAtLeastOnceQoS())));
    }
}
