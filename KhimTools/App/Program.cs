using System;
using System.Windows.Forms;

namespace KhiemToolsApp
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // If executed directly by user without arguments, show informational guidance
            if (args == null || args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MessageBox.Show(
                    "K-TOOLS Updater is an internal component designed to run automatically from Revit.\n\n" +
                    "To update K-TOOLS, please launch Revit and click 'Check Update' in the K-TOOLS ribbon tab.",
                    "K-TOOLS Updater",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }

            return UpdaterController.RunCli(args);
        }
    }
}
