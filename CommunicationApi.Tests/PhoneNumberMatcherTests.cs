using Shared;

namespace CommunicationApi.Tests;

public sealed class PhoneNumberMatcherTests
{
    [Theory]
    [InlineData("+380501234567", "+380501234567")]
    [InlineData("+380501234567", "380501234567")]
    [InlineData("+380 50 123-45-67", "+380501234567")]
    [InlineData("+1 (555) 123-4567", "15551234567")]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("+380501234567", "0501234567")]
    [InlineData("00380501234567", "+380501234567")]
    [InlineData("900", "900")]
    [InlineData("0900", "900")]
    [InlineData("Kyivstar", "Kyivstar")]
    public void IsSameNumber_SameNumber_ReturnsTrue(string sender, string from)
    {
        Assert.True(PhoneNumberMatcher.IsSameNumber(sender, from));
    }

    [Theory]
    [InlineData("+380501234567", "+380501234568")]
    [InlineData("+380501234567", "+380671234567")]
    [InlineData("4567", "+380501234567")]
    [InlineData("+380501234567", "4567")]
    [InlineData("01234567", "+380501234567")]
    [InlineData("900", "901")]
    [InlineData("Kyivstar", "Vodafone")]
    [InlineData("Kyivstar", "+380501234567")]
    [InlineData("+380501234567", "")]
    [InlineData("+380501234567", null)]
    [InlineData("", "+380501234567")]
    [InlineData(null, "+380501234567")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void IsSameNumber_DifferentOrMissingNumber_ReturnsFalse(string? sender, string? from)
    {
        Assert.False(PhoneNumberMatcher.IsSameNumber(sender, from));
    }
}
