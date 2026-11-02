using Refit;
using Shared;

namespace CommunicationApp.Services;

public interface IGatewayApi
{
    [Post("/send")]
    Task<IApiResponse> Send([Body] Message body);
}
