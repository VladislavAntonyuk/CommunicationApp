namespace CommunicationApp.Tests.Helpers;

public abstract class ApiTest : IAsyncDisposable
{
    protected ApiFactory Factory { get; } = new();

    protected HttpClient Client => field ??= Factory.CreateClient().Authorized(Factory.Services);

    protected HttpClient AnonymousClient => field ??= Factory.CreateClient();

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
