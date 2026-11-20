using Microsoft.Extensions.Options;
using Shared;
using Silverback.Messaging.Broker;

namespace CommunicationApp.Services;

public sealed class IncomingSmsForwarder(IProducerCollection producers, IOptions<MqttSettings> options)
{
    public async Task Forward(string text, string id, CancellationToken token) =>
        await producers.GetProducerForEndpoint(options.Value.IncomingTopic)
            .ProduceAsync(new IncomingSms(text, id), cancellationToken: token);
}
