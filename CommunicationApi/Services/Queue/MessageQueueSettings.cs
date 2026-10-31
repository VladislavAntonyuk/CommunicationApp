namespace CommunicationApi.Services.Queue;

public class MessageQueueSettings
{
    public required string Name { get; set; }
    public required int MaxMessages { get; set; }
}
