using System.Text.Json.Serialization;

namespace Shared;

[JsonConverter(typeof(JsonStringEnumConverter<ChatKind>))]
public enum ChatKind
{
    Private,
    Group,
    Channel
}
