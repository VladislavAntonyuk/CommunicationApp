using System.Text;
using Shared;
using Silverback.Messaging.Consuming.ErrorHandling;
using Silverback.Messaging.Messages;
using Silverback.Messaging.Publishing;

namespace CommunicationApp.Services;

public sealed class SkippedSmsReplier(MessageListener listener, IPublisher publisher) : ISkipMessageErrorPolicyCallback
{
    public Task OnSkippingMessageAsync(IRawInboundEnvelope envelope, Exception exception) =>
        listener.Reply(
            envelope.Headers.GetValue(MqttMessageHeaders.ResponseTopic),
            envelope.Headers.GetValue(MqttMessageHeaders.CorrelationData) is { } correlationData
                ? Encoding.UTF8.GetString(Convert.FromBase64String(correlationData))
                : null,
            new SmsAck(false, exception.Message),
            publisher);
}
