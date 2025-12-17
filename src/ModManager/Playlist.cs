using SimpleBase;

namespace ModManager;

public sealed record Playlist
{
    public required string Name { get; init; }

    public required ImmutableArray<PlaylistMod> Mods { get; init; }

    public string Serialize()
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Name);

                foreach (var mod in Mods)
                {
                    writer.Write(mod.Id);
                    writer.Write(mod.Enabled);
                }
            }

            ms.Position = 0;
            bytes = ms.ToArray();
        }

        var compressed = Zstd.Compress(bytes);

        byte[] bytesWithVersion = [1, ..compressed];
        var ascii85 = Base85.Ascii85.Encode(bytesWithVersion);
        return ascii85;
    }

    public static Playlist Deserialize(string encoded)
    {
        var bytes = Base85.Ascii85.Decode(encoded).ToArray();

        var version = bytes[0];
        var withoutVersion = bytes.AsSpan().Slice(1);

        if (version == 1)
        {
            return DeserializeVersion1(withoutVersion);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported version '{version}'.");
        }
    }

    private static Playlist DeserializeVersion1(in ReadOnlySpan<byte> bytes)
    {
        var decompressed = Zstd.Decompress(bytes).ToArray();
        using var ms = new MemoryStream(decompressed);
        using var reader = new BinaryReader(ms, Encoding.UTF8);
        var name = reader.ReadString();

        var mods = new List<PlaylistMod>();

        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            var id = reader.ReadUInt64();
            var enabled = reader.ReadBoolean();

            mods.Add(new()
            {
                Id = id,
                Enabled = enabled,
            });
        }

        return new Playlist
        {
            Name = name,
            Mods = mods.ToImmutableArray(),
        };
    }
}
