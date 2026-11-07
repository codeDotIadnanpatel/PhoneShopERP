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
        "passwd",
        "password",
        "pin",
        "pwd",
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
        "api_key",
        "api-key",
        "apikey",
        "connection_string",
        "connectionstring",
        "private_key",
        "privatekey",
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
        return RedactJson(document.RootElement);
    }

    /// <summary>
    /// Serialises a redacted copy of an already parsed JSON value. Audit detail
    /// is stored as <see cref="JsonDocument"/>, so this overload is the normal
    /// persistence path; parsing never happens twice.
    /// </summary>
    public static string RedactJson(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteRedacted(element, writer);
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
            // A nested object map must be redacted key-by-key, otherwise a
            // credential nested one level deeper would leak as a key/value pair.
            if (value is IEnumerable<KeyValuePair<string, object?>> map)
            {
                return Redact(map);
            }

            return sequence.Cast<object?>().Select(RedactValue).ToArray();
        }

        return value;
    }

    private static string RedactJsonString(string text) =>
        TryRedactEmbeddedJson(text, out var redacted) ? redacted : text;

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

            case JsonValueKind.String:
                // A string value can itself contain an encoded JSON document.
                // Redact it too, otherwise a caller that stores a provider
                // response or a serialised payload as a string would bypass the
                // key-name check one level down.
                var text = element.GetString()!;
                writer.WriteStringValue(
                    TryRedactEmbeddedJson(text, out var redacted) ? redacted : text);
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool TryRedactEmbeddedJson(string text, out string redacted)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.IsEmpty || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            redacted = text;
            return false;
        }

        try
        {
            redacted = RedactJson(text);
            return true;
        }
        catch (JsonException)
        {
            // Malformed metadata is preserved rather than failing or silently
            // dropping the audit event. It cannot contain a parsed credential
            // field, so there is nothing deterministic to redact.
            redacted = text;
            return false;
        }
    }
}
