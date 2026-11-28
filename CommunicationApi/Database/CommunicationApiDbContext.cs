using CommunicationApi.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace CommunicationApi.Database;

public class CommunicationApiDbContext(DbContextOptions<CommunicationApiDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<ProviderSetting> ProviderSettings => Set<ProviderSetting>();
    public DbSet<ChatSetting> ChatSettings => Set<ChatSetting>();
    public DbSet<OutgoingMessage> OutgoingMessages => Set<OutgoingMessage>();
    public DbSet<IncomingMessage> IncomingMessages => Set<IncomingMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationApiDbContext).Assembly);
        modelBuilder.Entity<ProviderSetting>().HasQueryFilter(x => x.UserId == currentUser.Id);
        modelBuilder.Entity<ChatSetting>().HasQueryFilter(x => x.UserId == currentUser.Id);
        base.OnModelCreating(modelBuilder);
    }
}