using SimpleBase;

namespace ModManager;

public sealed record Playlist
{
    private const int NewestSerializationFormat = 2;

    public required string Name { get; init; }

    public required ImmutableArray<PlaylistMod> Mods { get; init; }

    public string Serialize()
        => Serialize(NewestSerializationFormat);

    public string Serialize(int version)
    {
        var serialized = SerializeInternal(version);
        byte[] bytesWithVersion = [(byte)version, .. serialized];
        var ascii85 = Base85.Ascii85.Encode(bytesWithVersion);
        return ascii85;
    }

    private ReadOnlySpan<byte> SerializeInternal(int version)
        => version switch
        {
            1 => SerializeInternalVersion1(),
            2 => SerializeInternalVersion2(),
            _ => throw new InvalidOperationException($"Unsupported version '{version}'."),
        };

    private byte[] SerializeInternalVersion1()
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
        return compressed.ToArray();
    }

    private byte[] SerializeInternalVersion2()
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Name);
                writer.Write((ushort)Mods.Length);

                var enabled = new bool[Mods.Length + 1];

                var maxSize = (ulong)uint.MaxValue;
                var fitsUint = Mods
                    .All(m => m.Id <= maxSize);

                enabled[0] = !fitsUint;

                for (var i = 0; i < Mods.Length; i++)
                {
                    var mod = Mods[i];
                    enabled[i + 1] = mod.Enabled;
                }

                var flagBytes = BooleanFlagsToBytes(enabled);

                foreach (var flagByte in flagBytes)
                {
                    writer.Write(flagByte);
                }

                foreach (var mod in Mods)
                {
                    if (fitsUint)
                    {
                        writer.Write((uint)mod.Id);
                    }
                    else
                    {
                        writer.Write((ulong)mod.Id);
                    }
                }
            }

            ms.Position = 0;
            bytes = ms.ToArray();
        }

        var compressed = Zstd.Compress(bytes);
        return compressed.ToArray();
    }

    public static Playlist Deserialize(string encoded)
    {
        var bytes = Base85.Ascii85.Decode(encoded).ToArray();

        var version = bytes[0];
        var withoutVersion = bytes.AsSpan().Slice(1);

        return version switch
        {
            1 => DeserializeVersion1(withoutVersion),
            2 => DeserializeVersion2(withoutVersion),
            _ => throw new InvalidOperationException($"Unsupported version '{version}'."),
        };
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

    private static Playlist DeserializeVersion2(in ReadOnlySpan<byte> bytes)
    {
        var decompressed = Zstd.Decompress(bytes).ToArray();
        using var ms = new MemoryStream(decompressed);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        var name = reader.ReadString();
        var modCount = reader.ReadUInt16();
        var flagByteCount = (int)Math.Ceiling((modCount + 1) / 8f);
        
        var flagBytes = new byte[flagByteCount];
        for (var i = 0; i < flagBytes.Length; i++)
        {
            flagBytes[i] = reader.ReadByte();
        }

        var flags = BytesToBooleanFlags(flagBytes, modCount + 1);
        var usesUlong = flags[0];

        var mods = new PlaylistMod[modCount];
        for (var i = 0; i < mods.Length; i++)
        {
            var id = usesUlong
                ? reader.ReadUInt64()
                : reader.ReadUInt32();
            var enabled = flags[i + 1];

            mods[i] = new()
            {
                Id = id,
                Enabled = enabled,
            };
        }

        return new Playlist
        {
            Name = name,
            Mods = mods.ToImmutableArray(),
        };
    }

    private static byte[] BooleanFlagsToBytes(bool[] flags)
    {
        var requiredSize = (int)Math.Ceiling(flags.Length / 8f);
        var result = new byte[requiredSize];

        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i];
            if (!flag)
            {
                continue;
            }

            var resultIndex = i / 8;
            var resultByte = result[resultIndex];
            var resultByteIndex = i % 8;
            var updatedResultByte = (byte)(resultByte | (byte)(1 << resultByteIndex));
            result[resultIndex] = updatedResultByte;
        }

        return result;
    }

    private static bool[] BytesToBooleanFlags(byte[] bytes, int expectedFlags)
    {
        var result = new bool[expectedFlags];

        for (var i = 0; i < result.Length; i++)
        {
            var resultIndex = i / 8;
            var resultByte = bytes[resultIndex];
            var resultByteIndex = i % 8;

            var mask = (byte)(1 << resultByteIndex);
            var flag = ((byte)(resultByte & mask)) > 0;

            result[i] = flag;
        }

        return result;
    }
}
