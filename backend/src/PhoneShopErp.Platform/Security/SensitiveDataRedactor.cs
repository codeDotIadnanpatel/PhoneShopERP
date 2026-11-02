using System.Collections.Frozen;
using System.Text.Json;

namespace PhoneShopErp.Platform.Security;

/// <summary>
/// Redacts values that must never reach logs, traces, error payloads or audit
/// detail. Matching is by field name, case-insensitive substring, and covers the
/// credential classes named by the security baseline.
/// </summary>
public static class SensitiveDataRedactor
{
    public const string Redacted = "[REDACTED]";

    private static readonly FrozenSet<string> ExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "card_number",
        "cardnumber",
        "cvv",
        "cvc",
        "id_token",
        "passcode",
        "password",
        "refresh_token",
        "secret",
        "session_token",
        "token",
        "access_token",
        "otp",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] Fragments =
    [
        "password",
        "secret",
        "token",
        "passcode",
        "cvv",
        "cvc",
        "card",
        "credential",
        "authorization",
        "cookie",
    ];

    public static bool IsSensitive(string name)
    {
        if (ExactNames.Contains(name))
        {
            return true;
        }

        return Fragments.Any(fragment =>
            name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, object?> Redact(IEnumerable<KeyValuePair<string, object?>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            result[pair.Key] = IsSensitive(pair.Key) ? Redacted : RedactValue(pair.Value);
        }

        return result;
    }

    public static string RedactJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteRedacted(document.RootElement, writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static object? RedactValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return RedactJsonString(text);
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            return sequence.Cast<object?>().Select(RedactValue).ToArray();
        }

        return value;
    }

    private static string RedactJsonString(string text)
    {
        if (!text.TrimStart().StartsWith('{') && !text.TrimStart().StartsWith('['))
        {
            return text;
        }

        try
        {
            return RedactJson(text);
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsSensitive(property.Name))
                    {
                        writer.WriteStringValue(Redacted);
                    }
                    else
                    {
                        WriteRedacted(property.Value, writer);
                    }
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedacted(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
