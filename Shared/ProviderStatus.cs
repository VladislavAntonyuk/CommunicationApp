namespace Shared;

public record ProviderStatus(Target Target, bool Enabled, bool Configured);

public record ProviderSwitch(bool Enabled);
