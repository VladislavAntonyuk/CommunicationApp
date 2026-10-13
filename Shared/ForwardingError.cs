namespace Shared;

public static class ForwardingError
{
    public static string Describe(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: { } statusCode } => $"Server error ({(int)statusCode})",
        HttpRequestException or System.Net.WebException => "Could not reach the server",
        TaskCanceledException => "The server did not respond in time",
        _ => ex.Message
    };
}
