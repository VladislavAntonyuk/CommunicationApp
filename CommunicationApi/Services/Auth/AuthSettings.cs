namespace CommunicationApi.Services.Auth;

public class AuthSettings
{
    public required string DefaultRegion { get; set; }

    public required string[] AllowedRegions { get; set; }
}
