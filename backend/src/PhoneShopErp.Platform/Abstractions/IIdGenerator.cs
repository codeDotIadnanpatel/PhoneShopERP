using System.Security.Cryptography;

namespace PhoneShopErp.Platform.Abstractions;

public interface IIdGenerator
{
    Guid NewId();
}

/// <summary>
/// RFC 9562 UUID version 7. The owning application generates identifiers; the
/// database has no UUID default (ADR-0014). Identifiers are never authorisation.
/// </summary>
public sealed class UuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => NewId(DateTimeOffset.UtcNow);

    /// <summary>Deterministic timestamp overload, used by tests.</summary>
    public static Guid NewId(DateTimeOffset timestamp)
    {
        Span<byte> bytes = stackalloc byte[16];

        var milliseconds = timestamp.ToUnixTimeMilliseconds();
        if (milliseconds is < 0 or > 0x0000_FFFF_FFFF_FFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timestamp),
                "Timestamp is outside the 48-bit UUIDv7 range.");
        }

        bytes[0] = (byte)(milliseconds >> 40);
        bytes[1] = (byte)(milliseconds >> 32);
        bytes[2] = (byte)(milliseconds >> 24);
        bytes[3] = (byte)(milliseconds >> 16);
        bytes[4] = (byte)(milliseconds >> 8);
        bytes[5] = (byte)milliseconds;

        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);

        // version 7
        bytes[6] = (byte)(0x70 | (random[0] & 0x0F));
        bytes[7] = random[1];

        // variant 10xx
        bytes[8] = (byte)(0x80 | (random[2] & 0x3F));
        random[3..].CopyTo(bytes[9..]);

        return new Guid(bytes, bigEndian: true);
    }

    public static int Version(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return bytes[6] >> 4;
    }

    public static long TimestampMilliseconds(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        long result = 0;
        for (var index = 0; index < 6; index++)
        {
            result = (result << 8) | bytes[index];
        }

        return result;
    }
}
