using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.FamilyManager.Views;

namespace KhimTools.FamilyManager.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdFamilyManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp?.ActiveUIDocument;
            var doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("KhimTools", "No active Revit document found.");
                return Result.Cancelled;
            }

            try
            {
                var window = new FamilyManagerWindow(doc);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("KhimTools Family Manager", $"Error opening Family Manager:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
