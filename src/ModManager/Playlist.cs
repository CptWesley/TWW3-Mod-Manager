using SimpleBase;
using System.IO.Compression;

namespace ModManager;

public sealed record Playlist
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ImmutableArray<PlaylistMod> Mods { get; init; }

    public string Serialize()
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            using (var compressor = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                using (var writer = new BinaryWriter(compressor, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(Id);
                    writer.Write(Name);

                    foreach (var mod in Mods)
                    {
                        writer.Write(mod.Id);
                        writer.Write(mod.Enabled);
                    }
                }
            }

            ms.Position = 0;
            bytes = ms.ToArray();
        }

        var ascii85 = Base85.Ascii85.Encode(bytes);
        return ascii85;
    }

    public static Playlist Deserialize(string encoded)
    {
        var bytes = Base85.Ascii85.Decode(encoded).ToArray();
        using (var decompressed = new MemoryStream())
        {
            using (var compressed = new MemoryStream(bytes))
            {
                using (var decompressor = new GZipStream(compressed, CompressionMode.Decompress))
                {
                    decompressor.CopyTo(decompressed);
                }

            }

            decompressed.Position = 0;

            using var reader = new BinaryReader(decompressed);

            var playlistId = reader.ReadString();
            var name = reader.ReadString();

            var mods = new List<PlaylistMod>();

            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                var modId = reader.ReadUInt64();
                var enabled = reader.ReadBoolean();

                mods.Add(new()
                {
                    Id = modId,
                    Enabled = enabled,
                });
            }

            return new Playlist
            {
                Id = playlistId,
                Name = name,
                Mods = mods.ToImmutableArray(),
            };
        }
    }
}
