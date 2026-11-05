using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
            .WithTopicFilter(f => f.WithTopic("messages").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), token);

        await using var factory = new BrokerApiFactory(mosquitto.Broker.ToString());
        var response = await factory.CreateClient().PostAsJsonAsync("/notify/Teams", new Message("Teams|1|hello"), token);

        response.EnsureSuccessStatusCode();
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal("Teams|1|hello", JsonSerializer.Deserialize<OutgoingSms>(message.ConvertPayloadToString())!.Text);
        Assert.Matches("^[0-9a-f]{32}$", Encoding.UTF8.GetString(message.CorrelationData));
    }
}
