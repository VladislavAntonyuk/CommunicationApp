namespace CommunicationClient.Services;

public static class BackendAddress
{
    public const string Key = "backend";

    public static Uri? From(IPreferences preferences) =>
        Uri.TryCreate(preferences.Get(Key, ""), UriKind.Absolute, out var uri) ? uri : null;
}
