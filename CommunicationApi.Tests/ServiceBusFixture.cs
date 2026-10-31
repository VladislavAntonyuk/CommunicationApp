using Azure.Messaging.ServiceBus.Administration;
using CommunicationApi.Tests;
using Testcontainers.ServiceBus;

[assembly: AssemblyFixture(typeof(ServiceBusFixture))]

namespace CommunicationApi.Tests;

public sealed class ServiceBusFixture : IAsyncLifetime
{
    private readonly ServiceBusContainer _container =
        new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.0")
            .WithAcceptLicenseAgreement(true)
            .Build();

    public static string ConnectionString { get; private set; } = "";

    public static ServiceBusAdministrationClient Administration { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        Administration = new ServiceBusAdministrationClient(_container.GetHttpConnectionString());
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
