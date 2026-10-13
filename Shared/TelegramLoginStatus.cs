using System.Text.Json.Serialization;

namespace Shared;

[JsonConverter(typeof(JsonStringEnumConverter<TelegramLoginStep>))]
public enum TelegramLoginStep
{
    NotConfigured,
    Phone,
    Code,
    Password,
    LoggedIn
}

public record TelegramLoginStatus(TelegramLoginStep Step, string? Account = null);

public record TelegramLoginValue(string Value);
