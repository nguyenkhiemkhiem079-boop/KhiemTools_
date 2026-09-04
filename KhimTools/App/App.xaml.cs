using System;
using System.Linq;
using System.Windows;

namespace KhiemToolsApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // If command-line arguments contain --action, run in headless CLI mode
            if (e.Args.Length > 0 && e.Args.Any(a => a.StartsWith("--action", StringComparison.OrdinalIgnoreCase)))
            {
                int exitCode = UpdaterController.RunCli(e.Args);
                Shutdown(exitCode);
                return;
            }

            base.OnStartup(e);
        }
    }
}
