using System.Text.Json.Serialization;

namespace Shared.Api;

[JsonConverter(typeof(JsonStringEnumConverter<Target>))]
public enum Target
{
    Email,
    Telegram,
    Teams,
    Slack
}
