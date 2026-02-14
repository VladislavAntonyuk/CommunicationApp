using Microsoft.EntityFrameworkCore;

namespace CommunicationApi.Database;

public class CommunicationApiDbContext(DbContextOptions<CommunicationApiDbContext> options) : DbContext(options)
{
    public DbSet<CommunicationApiMessage> Messages => Set<CommunicationApiMessage>();
}