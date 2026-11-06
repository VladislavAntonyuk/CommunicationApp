using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CommunicationApi.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Shared;

namespace CommunicationApi.Tests;

public sealed class BrokerTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    [Fact]
    public async Task Notify_MessageReachesSubscriber()
    {
        var token = TestContext.Current.CancellationToken;

        await using var factory = new BrokerApiFactory(mosquitto.Broker.ToString());
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscriber = new MqttClientFactory().CreateMqttClient();
        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage);
            return Task.CompletedTask;
        };
        await subscriber.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build(), token);
        await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(factory.Topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), token);

        var response = await factory.CreateClient().PostAsJsonAsync("/notify/Teams", new Message("Teams|1|hello"), token);

        response.EnsureSuccessStatusCode();
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal("Teams|1|hello", JsonSerializer.Deserialize<OutgoingSms>(message.ConvertPayloadToString())!.Text);
        Assert.Matches("^[0-9a-f]{32}$", Encoding.UTF8.GetString(message.CorrelationData));
        Assert.Equal(factory.AckTopic, message.ResponseTopic);
        Assert.InRange(message.MessageExpiryInterval, 3590u, 3600u);
    }

    [Fact]
    public async Task Ack_ResolvesMessageOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new BrokerApiFactory(mosquitto.Broker.ToString());
        var response = await factory.CreateClient().PostAsJsonAsync("/notify/Telegram", new Message("Telegram|1|hello"), token);
        response.EnsureSuccessStatusCode();
        var id = (await Messages(factory)).Single().Id;
        using var gateway = new MqttClientFactory().CreateMqttClient();
        await gateway.ConnectAsync(new MqttClientOptionsBuilder()
            .WithConnectionUri(mosquitto.Broker)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build(), token);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while ((await Messages(factory)).Single().Sent == null)
        {
            Assert.True(DateTime.UtcNow < deadline, "The ack was not consumed");
            await PublishAck(gateway, factory.AckTopic, id, new SmsAck(true), token);
            await Task.Delay(200, token);
        }

        await PublishAck(gateway, factory.AckTopic, id, new SmsAck(false, "Late"), token);
        await Task.Delay(TimeSpan.FromSeconds(1), token);

        var message = (await Messages(factory)).Single();
        Assert.True(message.Sent);
        Assert.Null(message.Error);
    }

    private static async Task<List<OutgoingMessage>> Messages(BrokerApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>().OutgoingMessages.AsNoTracking().ToListAsync();
    }

    private static Task PublishAck(IMqttClient gateway, string topic, string id, SmsAck ack, CancellationToken token) =>
        gateway.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(ack))
            .WithCorrelationData(Encoding.UTF8.GetBytes(id))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), token);
}
