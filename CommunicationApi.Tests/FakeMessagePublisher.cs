using CommunicationApi.Services.Messaging;

namespace CommunicationApi.Tests;

public sealed class FakeMessagePublisher : IMessagePublisher
{
    public List<string> Published { get; } = [];

    public Task Publish(string text)
    {
        Published.Add(text);
        return Task.CompletedTask;
    }
}
