using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.DoorSchedule.Services;
using KhimTools.DoorSchedule.UI;
using System.Windows.Interop;
namespace KhimTools.DoorSchedule.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class CmdCreateDoorWindowSchedule : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (commandData.Application.Application.VersionNumber != "2025")
            {
                message = "DLL này chỉ hỗ trợ Revit 2025 (.NET 8, x64). Hãy chạy bằng Add-in Manager tương thích Revit 2025.";
                return Result.Failed;
            }
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc is null || uiDoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show("K-TOOL", "Mở một project Revit 2025 trước khi chạy lệnh.");
                return Result.Cancelled;
            }
            var doc = uiDoc.Document;
            if (doc.IsReadOnly || doc.IsModifiable)
            {
                message = "Project đang chỉ đọc hoặc có transaction đang mở. Dùng Add-in Manager Manual Mode, đóng thao tác chỉnh sửa đang chạy rồi thử lại.";
                return Result.Failed;
            }
            var builder = new ScheduleBuilder(doc, uiDoc.ActiveView);
            var window = new ScheduleOptionsWindow(builder, builder.Inspect());
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();
            if (window.ViewToOpen is { } id && doc.GetElement(new ElementId(id)) is View view)
            {
                // Schedule writes have committed and the modal window is closed before switching views.
                uiDoc.ActiveView = view;
            }
            return window.HasCommittedChanges ? Result.Succeeded : Result.Cancelled;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
        catch (Exception ex)
        {
            var log = LocalLog.Write(ex);
            message = ex.Message + (log is null ? "" : $"\nLog: {log}");
            return Result.Failed;
        }
    }
}
