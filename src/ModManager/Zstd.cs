using ZstdSharp;

namespace ModManager;

public static class Zstd
{
    [ThreadStatic]
    private static Compressor? compressor;

    [ThreadStatic]
    private static Decompressor? decompressor;

    public static Span<byte> Compress(in ReadOnlySpan<byte> uncompressed)
    {
        compressor ??= new(level: 7);
        return compressor.Wrap(uncompressed);
    }

    public static Span<byte> Decompress(in ReadOnlySpan<byte> compressed)
    {
        decompressor ??= new();
        return decompressor.Unwrap(compressed);
    }
}
