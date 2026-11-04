namespace CommunicationApp.Services;

public class MqttSettings
{
    public required Uri Broker { get; set; }

    public required string ClientId { get; set; }

    public required string Topic { get; set; }

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan SessionExpiry { get; set; } = TimeSpan.FromDays(7);
}
