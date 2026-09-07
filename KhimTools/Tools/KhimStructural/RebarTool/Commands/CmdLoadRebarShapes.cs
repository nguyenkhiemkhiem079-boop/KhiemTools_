using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    /// <summary>
    /// Lệnh mở giao diện Quản lý & Nạp Thư viện Rebar Shape (BS 8666 / JIS 43 Shapes) vào dự án hiện hành.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdLoadRebarShapes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc != null ? uidoc.Document : null;

                if (doc == null)
                {
                    TaskDialog.Show("Rebar Shape Library", "Vui lòng mở một dự án Revit trước khi thực hiện lệnh.");
                    return Result.Cancelled;
                }

                var window = new RebarShapeLoaderWindow(doc);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Rebar Shape Library Error", string.Format("[{0}] {1}\n\n{2}", ex.GetType().Name, ex.Message, ex.StackTrace));
                return Result.Failed;
            }
        }
    }
}
