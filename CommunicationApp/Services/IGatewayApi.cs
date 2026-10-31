using Refit;
using Shared;

namespace CommunicationApp.Services;

public interface IGatewayApi
{
    [Get("/receive")]
    Task<IApiResponse<List<ReceivedMessage>>> GetPending();

    [Post("/receive/ack")]
    Task<IApiResponse> Acknowledge([Body] Acknowledgement body);

    [Post("/send")]
    Task<IApiResponse> Send([Body] Message body);
}
