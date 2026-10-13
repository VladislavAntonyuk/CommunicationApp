using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared;

namespace CommunicationApi.Database;

// A provider without a row is enabled.
public class ProviderSetting
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Target Target { get; set; }
    public required bool Enabled { get; set; }
}
