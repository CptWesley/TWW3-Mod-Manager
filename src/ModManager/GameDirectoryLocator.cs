using Microsoft.Win32;

namespace ModManager;

public sealed class GameDirectoryLocator
{
    private static readonly Lazy<string?> lazyGamePath = new(GetGamePathInternal);
    private static readonly Lazy<string?> lazyWorkshopPath = new(GetWorkshopContentPathInternal);
    private static readonly Regex PathRegex = new Regex(@"\s*""path""\s*""([^""]*)""", RegexOptions.Compiled);
    private static readonly Regex AppRegex = new Regex(@"\s*""(\d+)""\s*""\d+""", RegexOptions.Compiled);

    public string? GamePath => lazyGamePath.Value;

    public string? WorkshopContentPath => lazyWorkshopPath.Value;

    private static string? GetWorkshopContentPathInternal()
    {
        if (lazyGamePath.Value is not { Length: > 0 } gamePath)
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(gamePath, $"../../workshop/content/{Constants.GameId}"));
    }

    private static string? GetGamePathInternal()
    {
        foreach (var library in GetSteamLibraryPaths())
        {
            var dir = Path.Combine(library, Constants.GameDirectoryName);
            var exe = Path.Combine(dir, Constants.GameExecutableName);

            if (File.Exists(exe))
            {
                return Path.GetFullPath(dir);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSteamLibraryPaths()
    {
        if (GetSteamPath() is not { } steamPath)
        {
            yield break;
        }

        var vdf = Path.Combine(steamPath, "steamapps/libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            string? path = null;

            foreach (var line in File.ReadAllLines(vdf))
            {
                if (PathRegex.Match(line) is { Success: true } pathMatch)
                {
                    path = pathMatch.Groups[1].Value;
                }
                else if (path is { Length: > 0 }
                      && AppRegex.Match(line) is { Success: true } appMatch
                      && int.TryParse(appMatch.Groups[1].Value, out var appId)
                      && appId == Constants.GameId)
                {
                    var candidate = Path.Combine(path, "steamapps/common");

                    if (Directory.Exists(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }
        else
        {
            var common = Path.Combine(steamPath, "steamapps/common");
            if (Directory.Exists(common))
            {
                yield return common;
            }
        }
    }

    private static string? GetSteamPath()
        => TryGetSteamPathFromRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam")
        ?? TryGetSteamPathFromRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam");

    private static string? TryGetSteamPathFromRegistry(string path)
    {
        if (GetRegistryValue(path) is not { } found)
        {
            return null;
        }

        var exe = Path.Combine(found, "steam.exe");

        if (!File.Exists(exe))
        {
            return found;
        }

        return found;
    }

    private static string? GetRegistryValue(string path)
        => Registry.GetValue(path, "InstallPath", null) is string { Length: > 0 } str
        ? str
        : null;
}
