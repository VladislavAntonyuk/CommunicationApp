using System.Text.Json.Serialization;

namespace Shared.Api;

[JsonConverter(typeof(JsonStringEnumConverter<ChatKind>))]
public enum ChatKind
{
    Private,
    Group,
    Channel
}
