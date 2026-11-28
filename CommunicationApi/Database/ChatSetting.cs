using Shared.Api;

namespace CommunicationApi.Database;

// Only chats the user has switched have a row. The rest follow the default for their kind.
public class ChatSetting
{
    public int UserId { get; set; }
    public Target Target { get; set; }
    public required string ChatId { get; set; }
    public required bool Enabled { get; set; }
}
