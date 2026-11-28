using CommunicationApi.Database;
using CommunicationApi.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Shared;
using Shared.Api;
using Silverback.Messaging.Publishing;

namespace CommunicationApi.Services;

public sealed class OutgoingSmsSender(CommunicationApiDbContext dbContext, IPublisher publisher, ICurrentUser currentUser)
{
    public async Task Send(Target target, Message message)
    {
        if (await dbContext.Users.FindAsync(currentUser.RequiredId) is not { } user)
            return;

        if (await dbContext.ProviderSettings.AnyAsync(x => x.Target == target && !x.Enabled))
            return;

        if (message.ChatId != null)
        {
            var saved = await dbContext.ChatSettings.FirstOrDefaultAsync(x => x.Target == target && x.ChatId == message.ChatId);
            if (!(saved?.Enabled ?? ChatDefaults.IsEnabled(message.ChatKind)))
                return;
        }

        var id = Guid.NewGuid().ToString("N");
        dbContext.OutgoingMessages.Add(new OutgoingMessage { Id = id, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        await publisher.PublishAsync(new OutgoingSms(user.PhoneNumber, message.Text, id));
    }
}
