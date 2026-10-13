using Shared;

namespace CommunicationApi.Services;

public static class ChatDefaults
{
    // Applies to a chat the user has not switched: private chats are received, groups and channels are not.
    public static bool IsEnabled(ChatKind? kind) => kind == ChatKind.Private;
}
