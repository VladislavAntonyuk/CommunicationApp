using Microsoft.EntityFrameworkCore;

namespace CommunicationApi.Database;

[Index(nameof(PhoneNumber), nameof(CreatedAt))]
public class LoginCode
{
    public int Id { get; set; }
    public required string PhoneNumber { get; set; }
    public required string CodeHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public int FailedAttempts { get; set; }
}
