namespace CommunicationApi.Tests;

public abstract class ApiTest : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = new();

    protected HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Factory.InitializeAsync();
        Client = Factory.CreateClient();
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
