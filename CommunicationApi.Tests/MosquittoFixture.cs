using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CommunicationApi.Tests;

public sealed class MosquittoFixture : IAsyncLifetime
{
    private const int Port = 1883;
    private const string Username = "test";
    private const string Password = "test";

    private readonly IContainer _container = new ContainerBuilder("eclipse-mosquitto:2.1.2-alpine")
        .WithPortBinding(Port, true)
        .WithResourceMapping(Config(), "/mosquitto/config/mosquitto.conf")
        .WithResourceMapping(new FileInfo("mosquitto.passwd"), "/mosquitto/config/")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(Port))
        .Build();

    public Uri Broker => new($"mqtt://{Username}:{Password}@{_container.Hostname}:{_container.GetMappedPublicPort(Port)}");

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private static byte[] Config() => Encoding.UTF8.GetBytes(
        File.ReadAllText("mosquitto.conf") + "\nallow_anonymous false\npassword_file /mosquitto/config/mosquitto.passwd\n");
}
