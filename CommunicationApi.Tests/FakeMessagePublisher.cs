using Silverback;
using Silverback.Messaging.Messages;
using Silverback.Messaging.Publishing;

namespace CommunicationApi.Tests;

public sealed class FakeMessagePublisher : IPublisher
{
    public List<string> Published { get; } = [];

    public ISilverbackContext Context => throw new NotSupportedException();

    public Task PublishAsync(object message, CancellationToken cancellationToken = default)
    {
        Published.Add(((StringMessage)message).Content!);
        return Task.CompletedTask;
    }

    public void Publish(object message, bool throwIfUnhandled = false) =>
        throw new NotSupportedException();

    public IReadOnlyCollection<TResult> Publish<TResult>(object message, bool throwIfUnhandled = false) =>
        throw new NotSupportedException();

    public Task PublishAsync(object message, bool throwIfUnhandled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<TResult>> PublishAsync<TResult>(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<TResult>> PublishAsync<TResult>(object message, bool throwIfUnhandled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}