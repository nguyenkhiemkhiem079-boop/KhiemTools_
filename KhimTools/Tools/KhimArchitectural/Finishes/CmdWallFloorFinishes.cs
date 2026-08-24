using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.Architectural.Finishes
{
    /// <summary>
    /// Command: Tự động bố trí lớp hoàn thiện sàn (Finish Floor) và ốp chân tường (Skirting)
    /// theo chu vi phòng (Room Boundary) cho các phòng đang chọn hoặc toàn bộ phòng trong View.
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

            try
            {
                // 1. Thu thập danh sách Phòng (Từ selection hoặc Active View)
                var selIds = uidoc.Selection.GetElementIds();
                var targetRooms = new List<Room>();

                foreach (var id in selIds)
                {
                    if (doc.GetElement(id) is Room r && r.Area > 0.05)
                    {
                        targetRooms.Add(r);
                    }
                }

                if (!targetRooms.Any())
                {
                    targetRooms = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WherePasses(new RoomFilter())
                        .Cast<Room>()
                        .Where(r => r.Area > 0.05)
                        .ToList();
                }

                if (!targetRooms.Any())
                {
                    TaskDialog.Show("KhimArchitectural — Finishes",
                        LanguageManager.IsEnglish
                            ? "No valid Rooms found in selection or current View."
                            : "Không tìm thấy Phòng (Room) hợp lệ nào trong vùng chọn hoặc View hiện hành.");
                    return Result.Cancelled;
                }

                // 2. Tìm FloorType thích hợp (Finish floor hoặc type đầu tiên)
                FloorType finishFloorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(ft => ft.Name.IndexOf("finish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          ft.Name.IndexOf("hoan thien", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          ft.Name.IndexOf("gach", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          ft.Name.IndexOf("tile", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().FirstOrDefault();

                if (finishFloorType == null)
                {
                    TaskDialog.Show("KhimArchitectural Error", "Không tìm thấy FloorType nào trong dự án.");
                    return Result.Failed;
                }

                int successFloors = 0;
                var boundaryOptions = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                };

                using (var tx = new Transaction(doc, "K-TOOLS: Generate Room Finishes"))
                {
                    tx.Start();

                    foreach (var room in targetRooms)
                    {
                        var boundarySegments = room.GetBoundarySegments(boundaryOptions);
                        if (boundarySegments == null || !boundarySegments.Any()) continue;

                        // Tạo CurveLoop từ vòng biên ngoài cùng
                        var outerLoop = new CurveLoop();
                        var firstRing = boundarySegments.First();

                        foreach (var segment in firstRing)
                        {
                            Curve c = segment.GetCurve();
                            if (c != null && c.Length > 0.01)
                            {
                                outerLoop.Append(c);
                            }
                        }

                        if (outerLoop.IsOpen() || !outerLoop.Any()) continue;

                        try
                        {
                            // Tạo sàn hoàn thiện cho phòng
                            Floor finishFloor = Floor.Create(doc, new List<CurveLoop> { outerLoop }, finishFloorType.Id, room.LevelId);
                            if (finishFloor != null)
                            {
                                // Gán comment nhận diện
                                var paramComment = finishFloor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                if (paramComment != null && !paramComment.IsReadOnly)
                                {
                                    paramComment.Set($"KhimFinishes: Room {room.Number} - {room.Name}");
                                }
                                successFloors++;
                            }
                        }
                        catch
                        {
                            // Bỏ qua nếu biên dạng phòng quá phức tạp hoặc tự giao cắt
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("KhimArchitectural — Room Finishes",
                    LanguageManager.IsEnglish
                        ? $"Successfully created {successFloors} Finish Floors for {targetRooms.Count} Rooms using [{finishFloorType.Name}]."
                        : $"Đã tạo thành công {successFloors} Sàn hoàn thiện cho {targetRooms.Count} Phòng với loại [{finishFloorType.Name}].");

                return Result.Succeeded;
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
