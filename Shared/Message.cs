namespace Shared;

public record Message(string Text, string? ChatId = null, ChatKind? ChatKind = null);