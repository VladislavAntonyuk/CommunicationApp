using System.Text;
using System.Text.Json;
using CommunicationApi.Database;
using CommunicationApi.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Shared;

namespace CommunicationApi.Tests;

public sealed class IncomingSmsTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sms_IsSentAndRecorded()
    {
        await using var factory = await Start();
        await Publish(factory, "Telegram|123|hello", "id1");

        var message = await WaitForResult(factory, "id1");

        Assert.True(message.Sent);
        Assert.Null(message.Error);
        Assert.Equal([new SentMessage(Target.Telegram, "123", null, "hello")], factory.Senders.Sent);
    }

    [Fact]
    public async Task SameIdTwice_IsSentOnce()
    {
        await using var factory = await Start();
        await Publish(factory, "Telegram|123|hello", "id1");
        await Publish(factory, "Telegram|123|hello", "id1");
        await Publish(factory, "Telegram|123|next", "id2");

        await WaitForResult(factory, "id2");

        Assert.Equal(["hello", "next"], factory.Senders.Sent.Select(sent => sent.Text));
    }

    [Fact]
    public async Task InvalidText_IsRecordedFailedWithoutSending()
    {
        await using var factory = await Start();
        await Publish(factory, "Viber|123|hello", "id1");

        var message = await WaitForResult(factory, "id1");

        Assert.False(message.Sent);
        Assert.Equal("Unknown target 'Viber'", message.Error);
        Assert.Equal(0, factory.Senders.Attempts);
    }

    [Fact]
    public async Task PermanentError_IsRecordedFailedWithoutRetry()
    {
        await using var factory = await Start();
        factory.Senders.Exception = new TelegramLoginException("Telegram is not logged in");
        await Publish(factory, "Telegram|123|hello", "id1");

        var message = await WaitForResult(factory, "id1");

        Assert.False(message.Sent);
        Assert.Equal("Telegram is not logged in", message.Error);
        Assert.Equal(1, factory.Senders.Attempts);
    }

    [Fact]
    public async Task TemporaryError_IsRetriedThenRecordedFailed()
    {
        await using var factory = await Start();
        factory.Senders.Exception = new InvalidOperationException("Network is down");
        await Publish(factory, "Telegram|123|hello", "id1");

        var message = await WaitForResult(factory, "id1");

        Assert.False(message.Sent);
        Assert.Equal("Network is down", message.Error);
        Assert.Equal(Services.Messaging.BrokerClientsConfigurator.MaxRetries + 1, factory.Senders.Attempts);
    }

    private async Task<BrokerApiFactory> Start()
    {
        var factory = new BrokerApiFactory(mosquitto.Broker.ToString());
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithClientId(factory.ClientId)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanStart(false)
            .WithSessionExpiryInterval(60)
            .Build(), Token);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(factory.IncomingTopic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
        _ = factory.Services;
        return factory;
    }

    private async Task Publish(BrokerApiFactory factory, string text, string id)
    {
        using var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build(), Token);
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(factory.IncomingTopic)
            .WithPayload(JsonSerializer.Serialize(new IncomingSms(text)))
            .WithCorrelationData(Encoding.UTF8.GetBytes(id))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), Token);
        await client.DisconnectAsync(cancellationToken: Token);
    }

    private static async Task<IncomingMessage> WaitForResult(BrokerApiFactory factory, string id)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var message = await scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>().IncomingMessages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, Token);
                if (message != null)
                    return message;
            }

            Assert.True(DateTime.UtcNow < deadline, $"Incoming SMS {id} was not processed");
            await Task.Delay(100, Token);
        }
    }
}
