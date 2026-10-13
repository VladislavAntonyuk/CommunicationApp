using System.Text.Json.Serialization;

namespace Shared;

[JsonConverter(typeof(JsonStringEnumConverter<Target>))]
public enum Target
{
    Email,
    Telegram,
    Teams,
    Slack
}
