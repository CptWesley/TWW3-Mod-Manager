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

            if (playlist.Name.Length > 0)
            {
                WriteToDisk();
            }
            else
            {
                usedMods.Set(playlist);
            }
        }
    }

    public ImmutableArray<Playlist> Get()
    {
        lock (lck)
        {
            return map.Values.ToImmutableArray();
        }
    }

    private void AddToMap(Playlist playlist)
        => map[playlist.Name] = playlist;

    private void WriteToDisk()
    {
        var sb = new StringBuilder();

        foreach (var playlist in map.Values.Where(static p => p.Name.Length > 0).OrderBy(static p => p.Name))
        {
            sb.AppendLine(playlist.Serialize());
        }

        var content = sb.ToString();
        File.WriteAllText(FileName, content);
    }

    private void LoadFromDisk()
    {
        map.Clear();
        var defaultPlaylist = usedMods.GetDefaultPlaylist();
        AddToMap(defaultPlaylist);

        if (!File.Exists(FileName))
        {
            return;
        }

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
}
