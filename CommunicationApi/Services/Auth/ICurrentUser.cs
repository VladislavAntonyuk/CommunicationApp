namespace CommunicationApi.Services.Auth;

public interface ICurrentUser
{
    int? Id { get; }

    int RequiredId => Id ?? throw new InvalidOperationException("There is no current user");

    void Set(int userId);
}
