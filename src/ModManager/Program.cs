using Steamworks;

namespace ModManager;

public static class Program
{
    [STAThread]
    public static void Main(params string[] args)
    {
        var exeDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location);
        Directory.SetCurrentDirectory(exeDirectory);

        NativeMethods.SetProcessDPIAware();
        Application.EnableVisualStyles();
        SteamClient.Init(Constants.GameId);

        var services = BuildServiceContainer();
        var form = services.GetRequiredService<LauncherForm>();

        Application.Run(form);
        SteamClient.Shutdown();
    }

    private static IServiceProvider BuildServiceContainer()
    {
        var services = new ServiceCollection();
        Configure(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void Configure(IServiceCollection services)
        => services
            .AddSingleton<LauncherForm>()
            .AddSingleton<GameDirectoryLocator>()
            .AddSingleton<GameLauncher>()
            .AddSingleton<UsedMods>()
            .AddSingleton<Workshop>()
            .AddSingleton<Playlists>();

    private static class NativeMethods
    {
        [DllImport("user32")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetProcessDPIAware();
    }
}
