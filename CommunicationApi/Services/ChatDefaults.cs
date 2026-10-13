using Shared;

namespace CommunicationApi.Services;

public static class ChatDefaults
{
    public static bool IsEnabled(ChatKind? kind) => kind == ChatKind.Private;
}
