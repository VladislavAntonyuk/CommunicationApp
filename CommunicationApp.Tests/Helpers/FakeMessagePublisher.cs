using Shared;
using Silverback;
using Silverback.Messaging.Publishing;

namespace CommunicationApp.Tests.Helpers;

public sealed class FakeMessagePublisher : IPublisher
{
    public List<OutgoingSms> Sms { get; } = [];

    public List<string> Published => [.. Sms.Select(sms => sms.Text)];

    public ISilverbackContext Context => throw new NotSupportedException();

    public Task PublishAsync(object message, CancellationToken cancellationToken = default)
    {
        Sms.Add((OutgoingSms)message);
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