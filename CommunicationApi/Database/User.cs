using Microsoft.EntityFrameworkCore;

namespace CommunicationApi.Database;

[Index(nameof(PhoneNumber), IsUnique = true)]
public class User
{
    public int Id { get; set; }
    public required string PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
