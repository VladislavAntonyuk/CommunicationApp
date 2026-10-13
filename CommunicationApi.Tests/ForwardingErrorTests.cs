using System.Net;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ForwardingErrorTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "Server error (502)")]
    [InlineData(HttpStatusCode.InternalServerError, "Server error (500)")]
    [InlineData(HttpStatusCode.NotFound, "Server error (404)")]
    public void Describe_ServerRespondedWithError_ShowsStatusCode(HttpStatusCode statusCode, string expected)
    {
        var ex = new HttpRequestException("Response status code does not indicate success", null, statusCode);

        Assert.Equal(expected, ForwardingError.Describe(ex));
    }

    [Fact]
    public void Describe_NoResponse_SaysServerNotReached()
    {
        Assert.Equal("Could not reach the server", ForwardingError.Describe(new HttpRequestException("No such host is known")));
        Assert.Equal("Could not reach the server", ForwardingError.Describe(new WebException("Connection refused")));
    }

    [Fact]
    public void Describe_Timeout_SaysServerDidNotRespond()
    {
        Assert.Equal("The server did not respond in time", ForwardingError.Describe(new TaskCanceledException()));
    }

    [Fact]
    public void Describe_OtherError_ShowsItsMessage()
    {
        Assert.Equal("Could not send SMS: no service", ForwardingError.Describe(new InvalidOperationException("Could not send SMS: no service")));
    }
}
