namespace CommunicationApi.Services.Auth;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private int? _id;

    public int? Id => _id ?? (httpContextAccessor.HttpContext?.User is { } user ? UserPrincipal.UserId(user) : null);

    public void Set(int userId) => _id = userId;
}
