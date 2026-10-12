namespace Shared;

public static class PhoneNumberMatcher
{
    public static bool IsSameNumber(string? sender, string? from)
    {
        if (!string.IsNullOrEmpty(sender) && sender == from)
            return true;

        var a = Digits(sender);
        var b = Digits(from);
        if (a.Length == 0 || b.Length == 0)
            return false;

        // The same number may arrive with or without a country code.
        var (longer, shorter) = a.Length >= b.Length ? (a, b) : (b, a);
        return longer == shorter || (shorter.Length >= 9 && longer.EndsWith(shorter));
    }

    private static string Digits(string? number) =>
        new string((number ?? "").Where(char.IsDigit).ToArray()).TrimStart('0');
}
