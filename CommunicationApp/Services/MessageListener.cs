using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Shared;
using Silverback.Configuration;
using Silverback.Messaging.Broker;
using Silverback.Messaging.Broker.Callbacks;
using Silverback.Messaging.Configuration;
using Silverback.Messaging.Configuration.Mqtt;
using Silverback.Messaging.Messages;
using Silverback.Messaging.Publishing;

namespace CommunicationApp.Services;

public sealed class MessageListener(
    IBrokerClientsConnector connector,
    IConsumerCollection consumers,
    IMessageHandler handler,
    IOptions<MqttSettings> options) : IMqttClientConnectedCallback
{
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, DateTime> _handled = new();

    public async Task Run(CancellationToken token)
    {
        await connector.ConnectAsync(token);
        try
        {
            using var timer = new PeriodicTimer(StatusInterval);
            while (await timer.WaitForNextTickAsync(token))
            {
                if (consumers.Any(consumer => consumer.StatusInfo.Status < ConsumerStatus.Connected))
                    handler.Failed("Not connected to the broker");

                await connector.ConnectAsync(token);
            }
        }
        finally
        {
            await connector.DisconnectAsync();
        }
    }

    public async Task OnMessage(IInboundEnvelope<OutgoingSms> envelope, IPublisher publisher)
    {
        var id = envelope.GetMqttCorrelationDataAsString();
        if (id != null && _handled.ContainsKey(id))
            return;

        SmsAck ack;
        try
        {
            await handler.Handle(envelope.Message!.Text);
            ack = new SmsAck(true);
        }
        catch (TimeoutException ex)
        {
            ack = new SmsAck(false, ex.Message);
        }

        await Reply(envelope.GetMqttResponseTopic(), id, ack, publisher);
    }

    public Task OnClientConnectedAsync(MqttClientConfiguration configuration)
    {
        handler.Connected();
        return Task.CompletedTask;
    }

    public async Task Reply(string? responseTopic, string? id, SmsAck ack, IPublisher publisher)
    {
        MarkHandled(id);
        if (responseTopic is { Length: > 0 })
            await publisher.WrapAndPublishAsync(ack, outbound => outbound
                .SetMqttDestinationTopic(responseTopic)
                .SetMqttCorrelationData(id));
    }

    private void MarkHandled(string? id)
    {
        if (id == null)
            return;

        var now = DateTime.UtcNow;
        foreach (var (handledId, handledAt) in _handled)
        {
            if (now - handledAt > options.Value.SessionExpiry)
                _handled.TryRemove(handledId, out _);
        }

        _handled.TryAdd(id, now);
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
            .AddDelegateSubscriber((IInboundEnvelope<OutgoingSms> envelope, MessageListener listener, IPublisher publisher) =>
                listener.OnMessage(envelope, publisher))
            .AddScopedErrorPolicyCallback<SkippedSmsReplier>();
        return services;
    }
}
