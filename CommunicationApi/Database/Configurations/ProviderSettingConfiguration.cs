using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunicationApi.Database.Configurations;

public class ProviderSettingConfiguration : IEntityTypeConfiguration<ProviderSetting>
{
    public void Configure(EntityTypeBuilder<ProviderSetting> builder)
    {
        builder.HasKey(x => new { x.UserId, x.Target });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}
