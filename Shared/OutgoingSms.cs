using System.Text.Json.Serialization;

namespace Shared;

public record OutgoingSms(string To, string Text, [property: JsonIgnore] string Id = "");
