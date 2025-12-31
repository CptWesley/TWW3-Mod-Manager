namespace ModManager;

public sealed class UsedMods(GameDirectoryLocator gameLocator, Workshop workshop)
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

                if (!Directory.Exists(dir))
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

        var subscribed = workshop.GetSubscribedItems().GetAwaiter().GetResult()
            .ToDictionary(x => x.Id, x => x.Directory);

        foreach (var mod in playlist.Mods.Where(static m => m.Enabled))
        {
            if (!subscribed.TryGetValue(mod.Id, out var modDir) || !Directory.Exists(modDir) || true)
            {
                var workshopDir = subscribed
                    .Select(static x => Path.GetDirectoryName(x.Value))
                    .Where(static x => Directory.Exists(x))
                    .FirstOrDefault();

                if (!Directory.Exists(workshopDir))
                {
                    modDir = null;
                }
                else
                {
                    modDir = Path.Combine(workshopDir, mod.Id.ToString());
                }
            }

            if (string.IsNullOrWhiteSpace(modDir) || !Directory.Exists(modDir))
            {
                modDir = Path.Combine(gameLocator.WorkshopContentPath, mod.Id.ToString());
            }

            if (!Directory.Exists(modDir))
            {
                Console.Error.WriteLine($"Couldn't find mod directory of '{mod.Id}'.");
                continue;
            }

            var modPacks = Directory.GetFiles(modDir, "*.pack");

            if (modPacks.Length > 0)
            {
                packs.AddRange(modPacks.Select(x => Path.GetFileName(x)));
                sb.AppendLine($"add_working_directory \"{modDir}\";");
            }
            else
            {
                Console.Error.WriteLine($"Couldn't find single .pack file in directory '{modDir}'.");
            }
        }

        foreach (var pack in packs.Distinct())
        {
            sb.AppendLine($"mod \"{pack}\";");
        }

        return sb.ToString();
    }
}
