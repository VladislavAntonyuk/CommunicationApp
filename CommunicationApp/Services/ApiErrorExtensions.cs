using Refit;

namespace CommunicationApp.Services;

public static class ApiErrorExtensions
{
    extension(IApiResponse response)
    {
        public string ErrorMessage => response.Error switch
        {
            ValidationApiException { Content.Detail: { Length: > 0 } detail } => detail,
            ApiException error => $"Server error ({(int)error.StatusCode})",
            ApiRequestException { InnerException: OperationCanceledException } => "The server did not respond in time",
            ApiRequestException => "Could not reach the server",
            { } error => error.Message,
            null => "The server sent no data"
        };
    }
}
