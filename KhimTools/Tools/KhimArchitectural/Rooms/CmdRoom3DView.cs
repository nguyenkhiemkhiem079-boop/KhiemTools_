using System;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Architectural;
using KhimTools.Core;

namespace KhimTools.Architectural.Rooms
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdRoom3DView : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var timer = Stopwatch.StartNew();
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                Room room = uidoc.Selection.GetElementIds().Select(doc.GetElement).OfType<Room>()
                    .FirstOrDefault(x => x.Area > 0.01);
                if (room == null && doc.ActiveView != null)
                    room = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfCategory(BuiltInCategory.OST_Rooms).WherePasses(new RoomFilter())
                        .Cast<Room>().FirstOrDefault(x => x.Area > 0.01);
                if (room == null)
                {
                    TaskDialog.Show("KhimArchitectural", LanguageManager.IsEnglish
                        ? "No placed Room found in the current view or selection."
                        : "Không tìm thấy Phòng (Room) hợp lệ trong vùng chọn hoặc View hiện hành.");
                    return Result.Cancelled;
                }

                View3D view = Room3DViewService.Create(doc, room);
                uidoc.ActiveView = view;
                timer.Stop();
                ArchitecturalDiagnostics.Log(nameof(CmdRoom3DView), doc, "create-room-3d-view", 1, 1, 0, timer.Elapsed);
                TaskDialog.Show("KhimArchitectural", LanguageManager.IsEnglish
                    ? $"Created 3D Room View: {view.Name}"
                    : $"Đã tạo thành công Khung nhìn 3D Phòng: {view.Name}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                timer.Stop();
                message = ex.Message;
                ArchitecturalDiagnostics.Log(nameof(CmdRoom3DView), doc, "create-room-3d-view", 1, 0, 1,
                    timer.Elapsed, ex.GetType().FullName);
                TaskDialog.Show("KhimArchitectural Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
