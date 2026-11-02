using System.Security.Cryptography;
using System.Text;

namespace PhoneShopErp.Platform.Observability;

/// <summary>
/// Correlation identifiers are accepted from a caller only when they match the
/// documented character set and length, and are always echoed back. The value is
/// never used to make an authorisation decision.
/// </summary>
public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    private const int MinimumLength = 8;
    private const int MaximumLength = 128;

    public static string Resolve(string? candidate)
    {
        if (IsValid(candidate))
        {
            return candidate!;
        }

        return Guid.NewGuid().ToString("N");
    }

    public static bool IsValid(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (candidate.Length is < MinimumLength or > MaximumLength)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            var allowed = char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' or ':' or '.';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Stable, non-secret correlation value used for tests and audit assertions.
    /// </summary>
    public static string ForTest(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexStringLower(hash)[..32];
    }
}
