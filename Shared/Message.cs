namespace Shared;

public record Message(string Text)
{
    public Target Target
    {
        get
        {
            var textParts = Text.Split('|');
            if (textParts.Length < 3)
            {
                throw new ArgumentException("Invalid message format. Expected format: 'Target|Recipient|MessageText'");
            }

            return Enum.Parse<Target>(textParts[0], true);
        }
    }
    
    public string Recipient
    {
        get
        {
            var textParts = Text.Split('|');
            if (textParts.Length < 3)
            {
                throw new ArgumentException("Invalid message format. Expected format: 'Target|Recipient|MessageText'");
            }

            return textParts[1];
        }
    }
    
    public string MessageText
    {
        get
        {
            var textParts = Text.Split('|');
            if (textParts.Length < 3)
            {
                throw new ArgumentException("Invalid message format. Expected format: 'Target|Recipient|MessageText'");
            }

            return textParts[2];
        }
    }
}