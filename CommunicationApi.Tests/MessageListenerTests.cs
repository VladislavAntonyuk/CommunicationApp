using System.Text;
using System.Text.Json;
using CommunicationApp.Services;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Shared;

namespace CommunicationApi.Tests;

public sealed class MessageListenerTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly string _clientId = $"gateway-{Guid.NewGuid():N}";
    private readonly string _topic = $"messages/{Guid.NewGuid():N}";

    private string AckTopic => $"{_topic}/ack";

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

    private ServiceProvider NewServices(Uri broker, IMessageHandler handler) => new ServiceCollection()
        .AddLogging()
        .Configure<MqttSettings>(settings =>
        {
            settings.Broker = broker;
            settings.ClientId = _clientId;
            settings.Topic = _topic;
            settings.RetryDelay = TimeSpan.FromMilliseconds(10);
        })
        .AddSingleton(handler)
        .AddMessageListener()
        .BuildServiceProvider();

    private async Task RegisterSession()
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithClientId(_clientId)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanStart(false)
            .WithSessionExpiryInterval(60)
            .Build(), Token);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(_topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private async Task RunUntil(Recorder handled, TimeSpan? settle = null, Task? until = null)
    {
        await using var services = NewServices(mosquitto.Broker, handled);
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
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder().WithConnectionUri(mosquitto.Broker).Build(), Token);
        foreach (var text in texts)
        {
            await client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(JsonSerializer.Serialize(new OutgoingSms(text)))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(), Token);
        }

        await client.DisconnectAsync(cancellationToken: Token);
    }

    private async Task PublishRequest(string text, string id)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build(), Token);
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(_topic)
            .WithPayload(JsonSerializer.Serialize(new OutgoingSms(text)))
            .WithResponseTopic(AckTopic)
            .WithCorrelationData(Encoding.UTF8.GetBytes(id))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private sealed class AckListener(IMqttClient client, int expected) : IAsyncDisposable
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(string, SmsAck)> _acks = [];

        public IReadOnlyList<(string, SmsAck)> Acks
        {
            get
            {
                lock (_acks)
                    return [.. _acks];
            }
        }

        public Task Done => _done.Task;

        public static async Task<AckListener> Start(Uri broker, string topic, int expected)
        {
            var client = new MqttClientFactory().CreateMqttClient();
            var listener = new AckListener(client, expected);
            client.ApplicationMessageReceivedAsync += listener.OnReceived;
            await client.ConnectAsync(new MqttClientOptionsBuilder()
                .WithConnectionUri(broker)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build(), Token);
            await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build(), Token);
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
            lock (_acks)
            {
                _acks.Add((id, ack));
                if (_acks.Count == expected)
                    _done.TrySetResult();
            }

            return Task.CompletedTask;
        }
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
