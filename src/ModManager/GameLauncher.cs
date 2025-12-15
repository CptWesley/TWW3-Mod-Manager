namespace ModManager;

public sealed class GameLauncher(GameDirectoryLocator gameLocator)
{
    private readonly object lck = new();

    public event EventHandler? GameLaunched;

    public event EventHandler? GameClosed;

    private Process? process;

    public void LaunchGame()
    {
        lock (lck)
        {
            if (process is { })
            {
                return;
            }

            var gamePath = gameLocator.GamePath;

            if (gamePath is null)
            {
                throw new InvalidOperationException("Game directory not found.");
            }

            var exe = Path.Combine(gamePath, Constants.GameExecutableName);

            if (!File.Exists(exe))
            {
                throw new InvalidOperationException("Executable not found.");
            }

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = gamePath,
                FileName = Constants.GameExecutableName,
            };

            process = Process.Start(startInfo);
            process.EnableRaisingEvents = true;

            GameLaunched?.Invoke(this, new());
            process.Exited += (s, e) =>
            {
                GameClosed?.Invoke(this, new());
                process.Dispose();
                process = null;
            };
        }
    }
}
