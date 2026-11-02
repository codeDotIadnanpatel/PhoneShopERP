using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhoneShopErp.Platform.Idempotency;

/// <summary>
/// Stable request fingerprint for idempotency comparisons. Object properties are
/// sorted so semantically identical JSON with different member order produces the
/// same fingerprint. Array order is preserved because it is meaningful.
/// </summary>
public static class RequestFingerprint
{
    public static string FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(document.RootElement, writer);
        }

        return Hash(buffer.ToArray());
    }

    public static string FromBytes(ReadOnlySpan<byte> bytes) => Hash(bytes);

    public static string FromString(string value) =>
        Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
        }
    }

    /// <summary>Opaque, non-sequential idempotency key used internally.</summary>
    public static string NewKey() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
