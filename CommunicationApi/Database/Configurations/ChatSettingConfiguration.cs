using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunicationApi.Database.Configurations;

public class ChatSettingConfiguration : IEntityTypeConfiguration<ChatSetting>
{
    public void Configure(EntityTypeBuilder<ChatSetting> builder)
    {
        builder.HasKey(x => new { x.UserId, x.Target, x.ChatId });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}
