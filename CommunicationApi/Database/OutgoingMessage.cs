namespace CommunicationApi.Database;

public class OutgoingMessage
{
    public required string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool? Sent { get; set; }
    public string? Error { get; set; }
}
