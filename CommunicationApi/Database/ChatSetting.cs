using Microsoft.EntityFrameworkCore;
using Shared;

namespace CommunicationApi.Database;

// Only chats the user has switched have a row. The rest follow the default for their kind.
[PrimaryKey(nameof(Target), nameof(ChatId))]
public class ChatSetting
{
    public Target Target { get; set; }
    public required string ChatId { get; set; }
    public required bool Enabled { get; set; }
}
