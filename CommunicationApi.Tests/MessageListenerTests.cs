using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using CommunicationApp.Services;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Shared;
using Silverback.Configuration;
using Silverback.Messaging.Consuming.ErrorHandling;
using Silverback.Messaging.Messages;

namespace CommunicationApi.Tests;

public sealed class MessageListenerTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly string _clientId = $"gateway-{Guid.NewGuid():N}";
    private readonly string _topic = $"messages/{Guid.NewGuid():N}";

    private string AckTopic => $"{_topic}/ack";

    private string IncomingTopic => $"{_topic}/incoming";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MessagesQueuedWhileOffline_AreHandledInOrder()
    {
        await RegisterSession();
        await Publish("first", "second", "third");
        var handled = new Recorder(3);

        await RunUntil(handled);

        Assert.Equal(new[] { "first", "second", "third" }, handled.Texts);
    }

    [Fact]
    public async Task HandledMessages_AreNotReturnedAgain()
    {
        await RegisterSession();
        await Publish("first");
        await RunUntil(new Recorder(1));
        var handled = new Recorder(0);

        await RunUntil(handled, TimeSpan.FromSeconds(1));

        Assert.Empty(handled.Texts);
    }

    [Fact]
    public async Task HandlerFailsOnce_RetriesAndKeepsOrder()
    {
        await RegisterSession();
        await Publish("first", "second");
        var failures = 1;
        var handled = new Recorder(2, () =>
        {
            if (failures-- > 0)
                throw new InvalidOperationException("No signal");
        });

        await RunUntil(handled);

        Assert.Equal(new[] { "first", "second" }, handled.Texts);
    }

    [Fact]
    public async Task HandlerKeepsFailing_SkipsMessageAndContinues()
    {
        await RegisterSession();
        await Publish("bad", "good");
        var handled = new Recorder(1, text =>
        {
            if (text == "bad")
                throw new InvalidOperationException("No signal");
        });

        await RunUntil(handled);

        Assert.Equal(new[] { "good" }, handled.Texts);
    }

    [Fact]
    public async Task HandledMessage_RepliesSentWithSameId()
    {
        await RegisterSession();
        await using var acks = await AckListener.Start(mosquitto.Broker, AckTopic, 1);
        await PublishRequest("first", "id1");

        await RunUntil(new Recorder(1), until: acks.Done);

        Assert.Equal([("id1", new SmsAck(true))], acks.Acks);
    }

    [Fact]
    public async Task HandlerKeepsFailing_RepliesFailedOnceAfterRetries()
    {
        await RegisterSession();
        await using var acks = await AckListener.Start(mosquitto.Broker, AckTopic, 1);
        await PublishRequest("bad", "id1");
        var handled = new Recorder(0, () => throw new InvalidOperationException("No signal"));

        await RunUntil(handled, until: acks.Done);

        Assert.Equal(BrokerClientsConfigurator.MaxRetries + 1, handled.Calls);
        Assert.Equal([("id1", new SmsAck(false, "No signal"))], acks.Acks);
    }

    [Fact]
    public async Task SameIdTwice_HandledAndRepliedOnce()
    {
        await RegisterSession();
        await using var acks = await AckListener.Start(mosquitto.Broker, AckTopic, 1);
        await PublishRequest("first", "id1");
        await PublishRequest("first again", "id1");
        var handled = new Recorder(1);

        await RunUntil(handled, TimeSpan.FromSeconds(1), acks.Done);

        Assert.Equal(new[] { "first" }, handled.Texts);
        Assert.Single(acks.Acks);
    }

    [Fact]
    public async Task SendResultTimesOut_RepliesFailedWithoutRetry()
    {
        await RegisterSession();
        await using var acks = await AckListener.Start(mosquitto.Broker, AckTopic, 1);
        await PublishRequest("first", "id1");
        var handled = new Recorder(0, () => throw new TimeoutException("No send result"));

        await RunUntil(handled, TimeSpan.FromSeconds(1), acks.Done);

        Assert.Equal(1, handled.Calls);
        Assert.Equal([("id1", new SmsAck(false, "No send result"))], acks.Acks);
    }

    [Fact]
    public async Task ErrorHandlingFails_ReconnectsAndContinues()
    {
        await RegisterSession();
        await using var acks = await AckListener.Start(mosquitto.Broker, AckTopic, 2);
        await PublishRequest("bad", "id1");
        await PublishRequest("good", "id2");
        var handled = new Recorder(1, text =>
        {
            if (text == "bad")
                throw new InvalidOperationException("No signal");
        });

        await RunUntil(handled, until: acks.Done, configure: services => services
            .AddSilverback()
            .AddSingletonErrorPolicyCallback(new FailingOnce()));

        Assert.Equal(new[] { "good" }, handled.Texts);
        Assert.Equal([("id1", new SmsAck(false, "No signal")), ("id2", new SmsAck(true))], acks.Acks);
    }

    [Fact]
    public async Task BrokerUnreachable_ReportsFailure()
    {
        var handler = new Recorder(0);
        await using var services = NewServices(new Uri("mqtt://localhost:1"), handler);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var running = services.GetRequiredService<MessageListener>().Run(stop.Token);
        var reason = await handler.FailedReason.WaitAsync(Timeout, Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.Equal("Not connected to the broker", reason);
    }

    [Fact]
    public async Task Forward_PublishesIncomingSmsWithId()
    {
        using var subscriber = await Connect(mosquitto.Broker);
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };
        await Subscribe(subscriber, IncomingTopic);
        var handler = new Recorder(0);
        await using var services = NewServices(mosquitto.Broker, handler);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var running = services.GetRequiredService<MessageListener>().Run(stop.Token);
        await handler.IsConnected.WaitAsync(Timeout, Token);

        await services.GetRequiredService<IncomingSmsForwarder>().Forward("Telegram|1|hello", "id1", Token);
        var message = await received.Task.WaitAsync(Timeout, Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.Equal("id1", Encoding.UTF8.GetString(message.CorrelationData));
        Assert.Equal("Telegram|1|hello", JsonSerializer.Deserialize<IncomingSms>(message.ConvertPayloadToString())!.Text);
    }

    [Fact]
    public async Task Forward_BeforeConnecting_FailsAndWorksAfterConnecting()
    {
        using var subscriber = await Connect(mosquitto.Broker);
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };
        await Subscribe(subscriber, IncomingTopic);
        var handler = new Recorder(0);
        await using var services = NewServices(mosquitto.Broker, handler);
        var forwarder = services.GetRequiredService<IncomingSmsForwarder>();
        using var early = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<Exception>(() => forwarder.Forward("Telegram|1|early", "id1", early.Token));

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var running = services.GetRequiredService<MessageListener>().Run(stop.Token);
        await handler.IsConnected.WaitAsync(Timeout, Token);
        await forwarder.Forward("Telegram|1|later", "id2", Token);
        var message = await received.Task.WaitAsync(Timeout, Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.Equal("id2", Encoding.UTF8.GetString(message.CorrelationData));
    }

    private ServiceProvider NewServices(Uri broker, IMessageHandler handler, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .Configure<MqttSettings>(settings =>
            {
                settings.Broker = broker;
                settings.ClientId = _clientId;
                settings.Topic = _topic;
                settings.IncomingTopic = IncomingTopic;
                settings.RetryDelay = TimeSpan.FromMilliseconds(10);
            })
            .AddSingleton(handler)
            .AddMessageListener();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private async Task RegisterSession()
    {
        using var client = await Connect(mosquitto.Broker, options => options
            .WithClientId(_clientId)
            .WithCleanStart(false)
            .WithSessionExpiryInterval(60));
        await Subscribe(client, _topic);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private async Task RunUntil(
        Recorder handled,
        TimeSpan? settle = null,
        Task? until = null,
        Action<IServiceCollection>? configure = null)
    {
        await using var services = NewServices(mosquitto.Broker, handled, configure);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var running = services.GetRequiredService<MessageListener>().Run(stop.Token);
        await handled.IsConnected.WaitAsync(Timeout, Token);
        await handled.Done.WaitAsync(Timeout, Token);
        if (until != null)
            await until.WaitAsync(Timeout, Token);
        await Task.Delay(settle ?? TimeSpan.FromMilliseconds(200), Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    private async Task Publish(params string[] texts)
    {
        using var client = await Connect(mosquitto.Broker);
        foreach (var text in texts)
            await client.PublishAsync(Sms(text).Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private async Task PublishRequest(string text, string id)
    {
        using var client = await Connect(mosquitto.Broker);
        await client.PublishAsync(Sms(text)
            .WithResponseTopic(AckTopic)
            .WithCorrelationData(Encoding.UTF8.GetBytes(id))
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private MqttApplicationMessageBuilder Sms(string text) => new MqttApplicationMessageBuilder()
        .WithTopic(_topic)
        .WithPayload(JsonSerializer.Serialize(new OutgoingSms(text)))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

    private static async Task<IMqttClient> Connect(Uri broker, Action<MqttClientOptionsBuilder>? configure = null)
    {
        var options = new MqttClientOptionsBuilder()
            .WithConnectionUri(broker)
            .WithProtocolVersion(MqttProtocolVersion.V500);
        configure?.Invoke(options);
        var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(options.Build(), Token);
        return client;
    }

    private static Task Subscribe(IMqttClient client, string topic) =>
        client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), Token);

    private sealed class AckListener(IMqttClient client, int expected) : IAsyncDisposable
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<(string, SmsAck)> _acks = new();

        public IReadOnlyList<(string, SmsAck)> Acks => [.. _acks];

        public Task Done => _done.Task;

        public static async Task<AckListener> Start(Uri broker, string topic, int expected)
        {
            var client = await Connect(broker);
            var listener = new AckListener(client, expected);
            client.ApplicationMessageReceivedAsync += listener.OnReceived;
            await Subscribe(client, topic);
            return listener;
        }

        public async ValueTask DisposeAsync()
        {
            await client.DisconnectAsync();
            client.Dispose();
        }

        private Task OnReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var id = Encoding.UTF8.GetString(e.ApplicationMessage.CorrelationData);
            var ack = JsonSerializer.Deserialize<SmsAck>(e.ApplicationMessage.ConvertPayloadToString())!;
            _acks.Enqueue((id, ack));
            if (_acks.Count == expected)
                _done.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOnce : ISkipMessageErrorPolicyCallback
    {
        private int _calls;

        public Task OnSkippingMessageAsync(IRawInboundEnvelope envelope, Exception exception) =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new InvalidOperationException("Callback failed")
                : Task.CompletedTask;
    }

    private sealed class Recorder(int expected, Action<string>? check = null) : IMessageHandler
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _failed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Recorder(int expected, Action check) : this(expected, _ => check())
        {
        }

        public List<string> Texts { get; } = [];

        public Task Done => expected == 0 ? Task.CompletedTask : _done.Task;

        public Task IsConnected => _connected.Task;

        public Task<string> FailedReason => _failed.Task;

        public int Calls { get; private set; }

        public Task Handle(string text)
        {
            Calls++;
            check?.Invoke(text);
            Texts.Add(text);
            if (Texts.Count == expected)
                _done.TrySetResult();
            return Task.CompletedTask;
        }

        public void Connected() => _connected.TrySetResult();

        public void Failed(string reason) => _failed.TrySetResult(reason);
    }
}
