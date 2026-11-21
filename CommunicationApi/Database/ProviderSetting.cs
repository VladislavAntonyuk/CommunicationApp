using Shared.Api;

namespace CommunicationApi.Database;

// A provider without a row is enabled.
public class ProviderSetting
{
    public Target Target { get; set; }
    public required bool Enabled { get; set; }
}
