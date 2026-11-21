using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunicationApi.Database.Configurations;

public class ChatSettingConfiguration : IEntityTypeConfiguration<ChatSetting>
{
    public void Configure(EntityTypeBuilder<ChatSetting> builder)
    {
        builder.HasKey(x => new { x.Target, x.ChatId });
    }
}
