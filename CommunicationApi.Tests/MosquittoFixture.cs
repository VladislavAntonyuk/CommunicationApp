using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunicationApi.Tests;

public sealed class MosquittoFixture : IAsyncLifetime
{
    private const int Port = 1883;

    private readonly IContainer _container = new ContainerBuilder("eclipse-mosquitto:2.1.2-alpine")
        .WithPortBinding(Port, true)
        .WithResourceMapping(new FileInfo("mosquitto.conf"), "/mosquitto/config/")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(Port))
        .Build();

    public Uri Broker => new($"mqtt://{_container.Hostname}:{_container.GetMappedPublicPort(Port)}");

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
