using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Forms;

namespace KhimTools.RuntimeQa.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdRuntimeQa : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                message = "K-TOOLS Runtime QA requires an open Revit project document.";
                return Result.Failed;
            }
            // No QA starts at Revit startup. The user explicitly selects Run All,
            // Run Suite or a fixture from the dashboard.
            var context = new RuntimeQaContext(uidoc);
            using (var form = new RuntimeQaForm(context, RuntimeQaRegistry.CreateDefault()))
            {
                form.ShowDialog();
            }
            return Result.Succeeded;
        }
    }
}
