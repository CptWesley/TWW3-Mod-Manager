using System.Runtime.InteropServices;

namespace ModManager;

public static class Program
{
    [STAThread]
    public static void Main(params string[] args)
    {
        NativeMethods.SetProcessDPIAware();
        Application.EnableVisualStyles();
        using var form = new LauncherForm();
        Application.Run(form);
    }

    private static class NativeMethods
    {
        [DllImport("user32")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool SetProcessDPIAware();
    }
}
