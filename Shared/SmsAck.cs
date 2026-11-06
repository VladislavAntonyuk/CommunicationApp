namespace Shared;

public record SmsAck(bool Sent, string? Error = null);
