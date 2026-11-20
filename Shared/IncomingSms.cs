using System.Text.Json.Serialization;

namespace Shared;

public record IncomingSms(string From, string Text, [property: JsonIgnore] string Id = "");
