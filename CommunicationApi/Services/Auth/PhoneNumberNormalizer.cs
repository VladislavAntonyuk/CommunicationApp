using PhoneNumbers;

namespace CommunicationApi.Services.Auth;

public static class PhoneNumberNormalizer
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public static string? ToE164(string? input, string defaultRegion, IEnumerable<string> allowedRegions)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        try
        {
            var number = Util.Parse(input, defaultRegion);
            if (Util.IsPossibleNumberWithReason(number) != PhoneNumberUtil.ValidationResult.IS_POSSIBLE)
                return null;

            return allowedRegions.Any(region => Util.GetCountryCodeForRegion(region) == number.CountryCode)
                ? Util.Format(number, PhoneNumberFormat.E164)
                : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
