using System.Net.Http.Json;
using MQTTnet;
using MQTTnet.Protocol;
using Shared;

namespace CommunicationApi.Tests;

public sealed class BrokerTests(MosquittoFixture mosquitto) : IClassFixture<MosquittoFixture>
{
    [Fact]
    public async Task Notify_MessageReachesSubscriber()
    {
        var token = TestContext.Current.CancellationToken;

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscriber = new MqttClientFactory().CreateMqttClient();
        subscriber.ApplicationMessageReceivedAsync += e =>
        {
            received.TrySetResult(e.ApplicationMessage.ConvertPayloadToString());
            return Task.CompletedTask;
        };
        await subscriber.ConnectAsync(new MqttClientOptionsBuilder().WithConnectionUri(mosquitto.Broker).Build(), token);
        await subscriber.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic("messages").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build(), token);

        await using var factory = new BrokerApiFactory(mosquitto.Broker.ToString());
        var response = await factory.CreateClient().PostAsJsonAsync("/notify/Teams", new Message("Teams|1|hello"), token);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Teams|1|hello", await received.Task.WaitAsync(TimeSpan.FromSeconds(10), token));
    }
}
