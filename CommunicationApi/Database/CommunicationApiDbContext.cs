using Microsoft.EntityFrameworkCore;

namespace CommunicationApi.Database;

public class CommunicationApiDbContext(DbContextOptions<CommunicationApiDbContext> options) : DbContext(options)
{
    public DbSet<ProviderSetting> ProviderSettings => Set<ProviderSetting>();
    public DbSet<ChatSetting> ChatSettings => Set<ChatSetting>();
    public DbSet<OutgoingMessage> OutgoingMessages => Set<OutgoingMessage>();
    public DbSet<IncomingMessage> IncomingMessages => Set<IncomingMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();
}