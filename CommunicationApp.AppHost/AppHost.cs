var builder = DistributedApplication.CreateBuilder(args);

var broker = builder.AddMosquitto("MessageBroker", port: 1883)
    .WithContainerFiles("/mosquitto/config",
    [
        new ContainerFile
        {
            Name = "mosquitto.conf",
            SourcePath = Path.Combine(builder.AppHostDirectory, "mosquitto", "mosquitto.conf")
        }
    ])
    .WithDataVolume();

builder.AddProject<Projects.CommunicationApi>("api")
    .WithReference(broker)
    .WaitFor(broker);

builder.Build().Run();
