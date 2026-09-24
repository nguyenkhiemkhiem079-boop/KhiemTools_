using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.SheetExport.Forms;

namespace KhimTools.SheetExport.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdSheetExport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc?.Document;
                if (doc == null) return Result.Cancelled;

                var form = new SheetExportForm(doc);
                form.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                System.Diagnostics.Debug.WriteLine("Sheet export command failed: " + ex);
                KhimDialogHelper.ShowError("Sheet Export Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
