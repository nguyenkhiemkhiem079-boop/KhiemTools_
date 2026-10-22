using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.Diagnostics.RevitSmoke
{
    /// <summary>
    /// Read-only command used to prove that Revit 2025 can load and invoke a small
    /// K-TOOLS assembly. It intentionally does not inspect or modify the active model.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class SmokeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                string revitVersion = commandData?.Application?.Application?.VersionNumber ?? "unknown";
                Version assemblyVersion = typeof(SmokeCommand).Assembly.GetName().Version;
                string versionText = assemblyVersion == null ? "unknown" : assemblyVersion.ToString();

                TaskDialog.Show(
                    "K-TOOLS Runtime Smoke",
                    "The K-TOOLS smoke assembly loaded and executed successfully.\n\n" +
                    "Revit version: " + revitVersion + "\n" +
                    "Smoke assembly version: " + versionText + "\n\n" +
                    "This command did not read or modify the active model.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "K-TOOLS smoke command failed: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
