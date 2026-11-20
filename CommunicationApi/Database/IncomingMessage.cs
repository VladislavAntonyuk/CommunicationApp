namespace CommunicationApi.Database;

public class IncomingMessage
{
    public required string Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool Sent { get; set; }
    public string? Error { get; set; }
}
