namespace Shared;


public record Message(string Text, Target Target);

public enum Target
{
    Sms,
    Email,
    Telegram
}