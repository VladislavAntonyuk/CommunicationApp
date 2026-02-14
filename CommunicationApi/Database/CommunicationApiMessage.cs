using Shared;

namespace CommunicationApi.Database;

public class CommunicationApiMessage
{
    public int Id { get; set; }
    public Target Target { get; set; }
    public required string Text { get; set; }
    public required DateTime DateTime { get; set; }
    public required bool IsRead { get; set; }
}