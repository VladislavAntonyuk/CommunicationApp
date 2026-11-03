using CommunicationApp.Services;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace CommunicationApi.Tests;

public sealed class MessageListenerTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly string _clientId = $"gateway-{Guid.NewGuid():N}";
    private readonly string _topic = $"messages/{Guid.NewGuid():N}";
    private readonly string _failedTopic = $"failed/{Guid.NewGuid():N}";

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
    public async Task HandlerKeepsFailing_MovesMessageAndContinues()
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
        Assert.Equal("bad", await ReadFailedMessage());
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
            settings.FailedTopic = _failedTopic;
            settings.RetryDelay = TimeSpan.FromMilliseconds(10);
        })
        .AddSingleton(handler)
        .AddMessageListener()
        .BuildServiceProvider();

    private async Task RegisterSession()
    {
        await Subscribe(_clientId, _topic);
        await Subscribe($"{_clientId}-failed", _failedTopic);
    }

    private async Task Subscribe(string clientId, string topic)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(SessionOptions(clientId), Token);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private MqttClientOptions SessionOptions(string clientId) => new MqttClientOptionsBuilder()
        .WithConnectionUri(mosquitto.Broker)
        .WithClientId(clientId)
        .WithProtocolVersion(MqttProtocolVersion.V500)
        .WithCleanStart(false)
        .WithSessionExpiryInterval(60)
        .Build();

    private async Task RunUntil(Recorder handled, TimeSpan? settle = null)
    {
        await using var services = NewServices(mosquitto.Broker, handled);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var running = services.GetRequiredService<MessageListener>().Run(stop.Token);
        await handled.IsConnected.WaitAsync(Timeout, Token);
        await handled.Done.WaitAsync(Timeout, Token);
        await Task.Delay(settle ?? TimeSpan.FromMilliseconds(200), Token);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    private async Task<string> ReadFailedMessage()
    {
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage.ConvertPayloadToString());
            return Task.CompletedTask;
        };

        await client.ConnectAsync(SessionOptions($"{_clientId}-failed"), Token);
        return await received.Task.WaitAsync(Timeout, Token);
    }

    private async Task Publish(params string[] texts)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder().WithConnectionUri(mosquitto.Broker).Build(), Token);
        foreach (var text in texts)
        {
            await client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(text)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(), Token);
        }

        await client.DisconnectAsync(cancellationToken: Token);
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

        public Task Handle(string text)
        {
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
