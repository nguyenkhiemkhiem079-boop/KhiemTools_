using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.Architectural.Rooms
{
    /// <summary>
    /// Command: Tự động tạo Khung nhìn 3D cô lập (3D Section Box) cho Phòng được chọn.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdRoom3DView : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                var selectedIds = uidoc.Selection.GetElementIds();
                Room targetRoom = null;

                foreach (var id in selectedIds)
                {
                    if (doc.GetElement(id) is Room room && room.Area > 0.01)
                    {
                        targetRoom = room;
                        break;
                    }
                }

                if (targetRoom == null)
                {
                    // Lấy phòng đầu tiên trong mô hình
                    targetRoom = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WherePasses(new RoomFilter())
                        .Cast<Room>()
                        .FirstOrDefault(r => r.Area > 0.01);
                }

                if (targetRoom == null)
                {
                    TaskDialog.Show("KhimArchitectural",
                        LanguageManager.IsEnglish
                            ? "No placed Room found in the current project."
                            : "Không tìm thấy Phòng (Room) hợp lệ trong dự án.");
                    return Result.Cancelled;
                }

                BoundingBoxXYZ roomBox = targetRoom.get_BoundingBox(null);
                if (roomBox == null) return Result.Cancelled;

                using (var tx = new Transaction(doc, "Create Room 3D View"))
                {
                    tx.Start();

                    // Tìm ViewFamilyType cho 3D View
                    ViewFamilyType vft3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                    if (vft3D != null)
                    {
                        View3D view3d = View3D.CreateIsometric(doc, vft3D.Id);
                        string baseName = $"3D_Room_{targetRoom.Number}_{targetRoom.Name}";
                        string viewName = baseName;
                        int idx = 1;
                        while (new FilteredElementCollector(doc).OfClass(typeof(View3D)).Any(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase)))
                        {
                            viewName = $"{baseName}_{idx++}";
                        }
                        view3d.Name = viewName;

                        // Mở rộng section box quanh phòng
                        var box = new BoundingBoxXYZ
                        {
                            Min = roomBox.Min - new XYZ(1.5, 1.5, 1.0),
                            Max = roomBox.Max + new XYZ(1.5, 1.5, 1.5)
                        };
                        view3d.SetSectionBox(box);
                        view3d.IsSectionBoxActive = true;

                        tx.Commit();
                        uidoc.ActiveView = view3d;

                        TaskDialog.Show("KhimArchitectural",
                            LanguageManager.IsEnglish
                                ? $"Created 3D Room View: {viewName}"
                                : $"Đã tạo thành công Khung nhìn 3D Phòng: {viewName}");
                        return Result.Succeeded;
                    }
                    tx.RollBack();
                }

                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("KhimArchitectural Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
