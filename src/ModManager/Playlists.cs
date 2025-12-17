namespace ModManager;

public sealed class Playlists
{
    private const string FileName = "playlists.txt";

    private readonly UsedMods usedMods;
    private readonly object lck = new();
    private readonly Dictionary<string, Playlist> map = new();

    public Playlists(UsedMods usedMods)
    {
        this.usedMods = usedMods;
        LoadFromDisk();
    }

    public void Save(Playlist playlist)
    {
        lock (lck)
        {
            map[playlist.Name] = playlist;
            WriteToDisk();
        }
    }

    public void Delete(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        lock (lck)
        {
            map.Remove(name);
            WriteToDisk();
        }
    }

    public ImmutableArray<Playlist> Get()
    {
        lock (lck)
        {
            return map.Values.ToImmutableArray();
        }
    }

    public Playlist? Get(string name)
    {
        lock (lck)
        {
            return map.TryGetValue(name, out var result) ? result : null;
        }
    }

    private void AddToMap(Playlist playlist)
        => map[playlist.Name] = playlist;

    private void WriteToDisk()
    {
        var sb = new StringBuilder();

        foreach (var playlist in map.Values.OrderBy(static p => p.Name))
        {
            sb.AppendLine(playlist.Serialize());
        }

        var content = sb.ToString();
        File.WriteAllText(FileName, content);
    }

    private void LoadFromDisk()
    {
        map.Clear();

        if (File.Exists(FileName))
        {
            foreach (var line in File.ReadAllLines(FileName))
            {
                try
                {
                    var playlist = Playlist.Deserialize(line);
                    AddToMap(playlist);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }

        if (!map.ContainsKey(string.Empty))
        {
            var defaultPlaylist = usedMods.GetDefaultPlaylist();
            AddToMap(defaultPlaylist);
            WriteToDisk();
        }
    }
}
