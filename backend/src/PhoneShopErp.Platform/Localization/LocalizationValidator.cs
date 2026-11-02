using System.Globalization;

namespace PhoneShopErp.Platform.Localization;

/// <summary>
/// Validation for the localization primitives Phase 01 stores. It deliberately
/// does not format money, choose a currency symbol or apply a tax rule: those
/// depend on AG-04 / AG-10 and the launch jurisdiction (D-009).
/// </summary>
public static class LocalizationValidator
{
    public static bool IsValidLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale) || locale.Length > 35)
        {
            return false;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            return !string.IsNullOrWhiteSpace(culture.Name)
                && !string.Equals(culture.Name, CultureInfo.InvariantCulture.Name, StringComparison.Ordinal);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates an IANA time-zone identifier. Offset strings such as "+02:00"
    /// are rejected: they cannot represent daylight-saving transitions.
    /// </summary>
    public static bool IsValidTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 64)
        {
            return false;
        }

        if (timeZone[0] is '+' or '-' || timeZone.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>
    /// ISO 3166-1 alpha-2 in upper case. Existence is checked against
    /// <c>platform.country</c> by the database foreign key, not a hard-coded list.
    /// </summary>
    public static bool IsValidCountryCode(string? code) =>
        code is { Length: 2 } && code.All(char.IsAsciiLetterUpper);
}
