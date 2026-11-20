using System.Text.Json.Serialization;

namespace Shared;

public record OutgoingSms(string Text, [property: JsonIgnore] string Id = "");
