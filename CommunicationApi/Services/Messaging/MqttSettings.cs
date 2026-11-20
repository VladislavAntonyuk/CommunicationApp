namespace CommunicationApi.Services.Messaging;

public class MqttSettings
{
    public required string ClientId { get; set; }

    public required string Topic { get; set; }

    public required string AckTopic { get; set; }

    public required string IncomingTopic { get; set; }

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public required TimeSpan MessageTtl { get; set; }

    public required TimeSpan SessionExpiry { get; set; }
}
