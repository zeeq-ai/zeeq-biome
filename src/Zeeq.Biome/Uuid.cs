namespace Zeeq.Biome;

internal static class Uuid
{
    internal static Guid CreateVersion7()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        // .NET 8 has no UUIDv7 factory. Preserve the same timestamp/random layout (RFC 9562).
        Span<byte> bytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(bytes, bigEndian: true, out _);
        var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var index = 5; index >= 0; index--)
        {
            bytes[index] = (byte)milliseconds;
            milliseconds >>= 8;
        }
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
#endif
    }
}
