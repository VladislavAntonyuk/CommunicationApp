using CommunicationApi.Services.Auth;

namespace CommunicationApp.Tests.Api;

public sealed class PhoneNumberNormalizerTests
{
    private static readonly string[] Allowed = ["UA", "US"];

    [Theory]
    [InlineData("+380501234567", "+380501234567")]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("050 123 45 67", "+380501234567")]
    [InlineData("+38 (050) 123-45-67", "+380501234567")]
    [InlineData("380501234567", "+380501234567")]
    [InlineData("+15555215556", "+15555215556")]
    public void AllowedNumber_ReturnsE164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.ToE164(input, "UA", Allowed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("12345")]
    [InlineData("+380123")]
    [InlineData("+999123456789")]
    public void InvalidNumber_ReturnsNull(string? input)
    {
        Assert.Null(PhoneNumberNormalizer.ToE164(input, "UA", Allowed));
    }

    [Theory]
    [InlineData("+48512345678")]
    [InlineData("+447911123456")]
    public void NumberFromOtherCountry_ReturnsNull(string input)
    {
        Assert.Null(PhoneNumberNormalizer.ToE164(input, "UA", Allowed));
    }

    [Fact]
    public void RegionNotAllowed_ReturnsNull()
    {
        Assert.Null(PhoneNumberNormalizer.ToE164("+15555215556", "UA", ["UA"]));
    }
}
