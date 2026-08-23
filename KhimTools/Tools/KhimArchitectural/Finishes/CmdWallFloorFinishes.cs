using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.Architectural.Finishes
{
    /// <summary>
    /// Command: Tự động bố trí lớp hoàn thiện sàn/tường theo chu vi phòng (Room Boundary).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdWallFloorFinishes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            int roomCount = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WherePasses(new RoomFilter())
                .Cast<Room>()
                .Count(r => r.Area > 0.01);

            TaskDialog.Show("KhimArchitectural — Finishes",
                LanguageManager.IsEnglish
                    ? $"Found {roomCount} valid Rooms ready for auto floor & wall finish generation."
                    : $"Tìm thấy {roomCount} Phòng hợp lệ sẵn sàng tạo lớp hoàn thiện sàn & ốp chân tường tự động.");

            return Result.Succeeded;
        }
    }
}
