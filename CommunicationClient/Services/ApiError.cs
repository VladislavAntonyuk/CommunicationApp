using Refit;
using Shared;

namespace CommunicationClient.Services;

public static class ApiError
{
    public static string Describe(Exception ex) => ex switch
    {
        ValidationApiException { Content.Detail: { Length: > 0 } detail } => detail,
        ApiException api => $"Server error ({(int)api.StatusCode})",
        _ => ForwardingError.Describe(ex)
    };
}
