using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunicationApi.Database.Configurations;

public class LoginCodeConfiguration : IEntityTypeConfiguration<LoginCode>
{
    public void Configure(EntityTypeBuilder<LoginCode> builder)
    {
        builder.HasIndex(x => new { x.PhoneNumber, x.CreatedAt });
    }
}
