namespace CommunicationApi.Database;

public class User
{
    public int Id { get; set; }
    public required string PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
