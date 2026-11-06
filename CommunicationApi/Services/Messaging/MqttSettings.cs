namespace CommunicationApi.Services.Messaging;

public class MqttSettings
{
    public required string ClientId { get; set; }

    public required string Topic { get; set; }

    public required string AckTopic { get; set; }
}
