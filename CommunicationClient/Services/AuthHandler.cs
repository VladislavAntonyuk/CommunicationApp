using System.Net;
using System.Net.Http.Headers;

namespace CommunicationClient.Services;

public sealed class AuthHandler(AuthSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        var accessToken = await session.GetAccessToken();
        if (accessToken == null)
            return await base.SendAsync(request, cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var newAccessToken = await session.RefreshAccessToken(accessToken);
        if (newAccessToken == null)
            return response;

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
