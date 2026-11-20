namespace CommunicationApp.Tests.Helpers;

public abstract class ApiTest : IAsyncDisposable
{
    protected ApiFactory Factory { get; } = new();

    protected HttpClient Client => field ??= Factory.CreateClient();

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
