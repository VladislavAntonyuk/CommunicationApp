var builder = DistributedApplication.CreateBuilder(args);

var serviceBus = builder.AddAzureServiceBus("MessageQueue").RunAsEmulator();
serviceBus.AddServiceBusQueue("messages")
    .WithProperties(queue => queue.LockDuration = TimeSpan.FromMinutes(1));

builder.AddProject<Projects.CommunicationApi>("api")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

builder.Build().Run();
