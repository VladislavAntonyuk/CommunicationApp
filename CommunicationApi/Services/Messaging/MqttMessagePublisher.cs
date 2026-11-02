using Silverback.Messaging.Messages;
using Silverback.Messaging.Publishing;

namespace CommunicationApi.Services.Messaging;

public class MqttMessagePublisher(IPublisher publisher) : IMessagePublisher
{
    public async Task Publish(string text)
    {
        await publisher.PublishAsync(new StringMessage(text));
    }
}
