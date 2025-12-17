namespace ModManager;

public sealed class UsedMods(GameDirectoryLocator gameLocator)
{
    private static readonly Regex WorkingDirectoryRegex = new(@"add_working_directory ""([^""]*)"";", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public void Set(Playlist playlist)
    {
        var content = CreateContent(playlist);
        var usedModsFile = Path.Combine(gameLocator.GamePath, "used_mods.txt");
        File.WriteAllText(usedModsFile, content);
    }

    public Playlist GetDefaultPlaylist()
    {
        var mods = new List<PlaylistMod>();
        var usedModsFile = Path.Combine(gameLocator.GamePath, "used_mods.txt");

        if (File.Exists(usedModsFile))
        {
            var lines = File.ReadAllLines(usedModsFile);
            foreach (var line in lines)
            {
                if (WorkingDirectoryRegex.Match(line) is not { Success: true } match)
                {
                    continue;
                }

                var dir = Path.GetFullPath(match.Groups[1].Value);

                if (!Directory.Exists(dir)
                 || dir.Length <= gameLocator.WorkshopContentPath!.Length
                 || !dir.StartsWith(gameLocator.WorkshopContentPath))
                {
                    continue;
                }

                var shortDir = Path.GetFileName(dir);

                if (shortDir is not { Length: > 0 } || !ulong.TryParse(shortDir, out var modId))
                {
                    continue;
                }

                mods.Add(new()
                {
                    Id = modId,
                    Enabled = true,
                });
            }
        }

        return new()
        {
            Name = string.Empty,
            Mods = mods.ToImmutableArray(),
        };
    }

    private string CreateContent(Playlist playlist)
    {
        var sb = new StringBuilder();

        var packs = new List<string>();

        foreach (var mod in playlist.Mods.Where(static m => m.Enabled))
        {
            var modDir = Path.Combine(gameLocator.WorkshopContentPath, mod.Id.ToString());

            if (!Directory.Exists(modDir))
            {
                continue;
            }

            var modPacks = Directory.GetFiles(modDir, "*.pack");

            if (modPacks.Length > 0)
            {
                packs.AddRange(modPacks.Select(x => Path.GetFileName(x)));
                sb.AppendLine($"add_working_directory \"{modDir}\";");
            }
        }

        foreach (var pack in packs.Distinct())
        {
            sb.AppendLine($"mod \"{pack}\";");
        }

        return sb.ToString();
    }
}
