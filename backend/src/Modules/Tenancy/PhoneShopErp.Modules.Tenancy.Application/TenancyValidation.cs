using System.Globalization;
using System.Text.RegularExpressions;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Localization;

namespace PhoneShopErp.Modules.Tenancy.Application;

/// <summary>
/// Input validation for tenancy-owned records. Validation never invents a
/// jurisdiction rule: locale and timezone are validated as formats, country
/// existence is enforced by a foreign key to reviewed reference data.
/// </summary>
public static partial class TenancyValidation
{
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public static string RequireSlug(string? value)
    {
        var slug = (value ?? string.Empty).Trim();
        if (!SlugPattern().IsMatch(slug))
        {
            throw Invalid("Slug must be 3-63 characters of lowercase letters, digits and hyphens, " +
                "and must start and end with a letter or digit.");
        }

        return slug;
    }

    public static string RequireDisplayName(string? value, string field)
    {
        var displayName = (value ?? string.Empty).Trim();
        if (displayName.Length is < 1 or > 200)
        {
            throw Invalid($"{field} must be 1-200 characters.");
        }

        return displayName;
    }

    public static string RequireCode(string? value, string field)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(code))
        {
            throw Invalid($"{field} must be 2-32 characters of uppercase letters, digits, hyphen or underscore.");
        }

        return code;
    }

    public static string RequireLocale(string? value)
    {
        if (!LocalizationValidator.IsValidLocale(value))
        {
            throw Invalid("Locale must be a valid BCP-47 culture name, for example 'en-GB'.");
        }

        return CultureInfo.GetCultureInfo(value!).Name;
    }

    public static string RequireTimezone(string? value)
    {
        if (!LocalizationValidator.IsValidTimeZone(value))
        {
            throw Invalid("Timezone must be an IANA time-zone identifier, for example 'Europe/London'. " +
                "Offset strings are not accepted.");
        }

        return value!;
    }

    public static string RequireCountryCode(string? value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!LocalizationValidator.IsValidCountryCode(code))
        {
            throw Invalid("Country must be an ISO 3166-1 alpha-2 code.");
        }

        return code;
    }

    public static string RequireLocationType(string? value)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (type is not ("store" or "warehouse" or "other"))
        {
            throw Invalid("Location type must be one of: store, warehouse, other.");
        }

        return type;
    }

    public static string RequireStatus(string? value, params string[] allowed)
    {
        var status = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!allowed.Contains(status, StringComparer.Ordinal))
        {
            throw Invalid($"Status must be one of: {string.Join(", ", allowed)}.");
        }

        return status;
    }

    public static string RequireReason(string? value)
    {
        var reason = (value ?? string.Empty).Trim();
        if (reason.Length is < 3 or > 500)
        {
            throw Invalid("Reason must be 3-500 characters.");
        }

        return reason;
    }

    private static PlatformException Invalid(string message) =>
        new(ErrorCodes.ValidationFailed, message, 400);
}
