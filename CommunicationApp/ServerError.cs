using System.Net.Http.Json;
using System.Text.Json;

namespace CommunicationApp;

public static class ServerError
{
    public static async Task<string> Read(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>();
            if (!string.IsNullOrEmpty(problem?.Detail))
                return problem.Detail;
        }
        catch (JsonException)
        {
        }

        return $"Server error ({(int)response.StatusCode})";
    }

    private sealed record Problem(string? Detail);
}
