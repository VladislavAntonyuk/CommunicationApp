namespace CommunicationApi.Services.Auth;

public class AuthSettings
{
    public required string DefaultRegion { get; set; }

    public required string[] AllowedRegions { get; set; }

    public required TimeSpan CodeLifetime { get; set; }

    public required TimeSpan ResendDelay { get; set; }

    public required int MaxCodesPerHour { get; set; }

    public required int MaxAttemptsPerCode { get; set; }
}
