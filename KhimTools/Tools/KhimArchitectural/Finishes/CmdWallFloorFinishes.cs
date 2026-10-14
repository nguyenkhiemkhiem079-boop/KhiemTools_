using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.Architectural;
using System.Diagnostics;

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
            var timer = Stopwatch.StartNew();
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
                int failedRooms = 0;
                int skippedExisting = 0;
                var existingFinishMarkers = new HashSet<string>(new FilteredElementCollector(doc)
                    .OfClass(typeof(Floor)).WhereElementIsNotElementType().Cast<Floor>()
                    .Select(x => x.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())
                    .Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.Ordinal);
                var boundaryOptions = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                };

                using (var tx = new Transaction(doc, "K-TOOLS: Generate Room Finishes"))
                {
                    TransactionBoundary.Start(tx, "Architectural.RoomFinishes");
                    try
                    {
                        foreach (var room in targetRooms)
                        {
                            string marker = "KhimFinishes: RoomUniqueId=" + room.UniqueId;
                            string legacyMarker = $"KhimFinishes: Room {room.Number} - {room.Name}";
                            if (existingFinishMarkers.Contains(marker) || existingFinishMarkers.Contains(legacyMarker))
                            { skippedExisting++; continue; }
                            var boundarySegments = room.GetBoundarySegments(boundaryOptions);
                            if (boundarySegments == null || !boundarySegments.Any()) { failedRooms++; continue; }
                            try
                            {
                                var profile = new List<CurveLoop>();
                                foreach (var ring in boundarySegments)
                                {
                                    var loop = new CurveLoop();
                                    foreach (var segment in ring)
                                    {
                                        Curve curve = segment.GetCurve();
                                        if (curve == null || curve.Length <= 0.01) throw new InvalidOperationException("Room boundary has a degenerate segment.");
                                        loop.Append(curve);
                                    }
                                    if (loop.IsOpen() || !loop.Any()) throw new InvalidOperationException("Room boundary ring is open or empty.");
                                    profile.Add(loop);
                                }
                                using (var sub = new SubTransaction(doc))
                                {
                                    TransactionBoundary.Start(sub, "Architectural.RoomFinishes");
                                    try
                                    {
                                        Floor finishFloor = Floor.Create(doc, profile, finishFloorType.Id, room.LevelId);
                                        if (finishFloor == null || doc.GetElement(finishFloor.Id) is not Floor || finishFloor.LevelId != room.LevelId)
                                            throw new InvalidOperationException("Finish floor failed its creation/level postcondition.");
                                        var paramComment = finishFloor.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                        if (paramComment == null || paramComment.IsReadOnly)
                                            throw new InvalidOperationException("Finish floor room marker is unavailable.");
                                        paramComment.Set(marker);
                                        if (!string.Equals(paramComment.AsString(), marker, StringComparison.Ordinal))
                                            throw new InvalidOperationException("Finish floor room marker failed its postcondition.");
                                        TransactionBoundary.Commit(sub, "Architectural.RoomFinishes");
                                        successFloors++;
                                        existingFinishMarkers.Add(marker);
                                    }
                                    catch
                                    {
                                        TransactionBoundary.RollBack(sub, "Architectural.RoomFinishes");
                                        failedRooms++;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Boundary extraction and profile construction do not mutate the document.
                                failedRooms++;
                            }
                        }

                        if (successFloors == 0) TransactionBoundary.RollBack(tx, "Architectural.RoomFinishes");
                        else TransactionBoundary.Commit(tx, "Architectural.RoomFinishes");
                    }
                    catch
                    {
                        TransactionBoundary.RollBack(tx, "Architectural.RoomFinishes");
                        throw;
                    }
                }

                timer.Stop();
                ArchitecturalDiagnostics.Log(nameof(CmdWallFloorFinishes), doc, "create-room-finish-floors",
                    targetRooms.Count, successFloors, failedRooms, timer.Elapsed);

                TaskDialog.Show("KhimArchitectural — Room Finishes",
                    LanguageManager.IsEnglish
                        ? $"Created {successFloors} Finish Floors for {targetRooms.Count} Rooms using [{finishFloorType.Name}]. Failed: {failedRooms}; already finished: {skippedExisting}."
                        : $"Đã tạo {successFloors} Sàn hoàn thiện cho {targetRooms.Count} Phòng với loại [{finishFloorType.Name}]. Lỗi: {failedRooms}; đã có: {skippedExisting}.");

                if (successFloors == 0 && skippedExisting == 0)
                    message = "No finish floors were created; all selected/visible Rooms were invalid or could not be converted.";
                return successFloors > 0 || skippedExisting > 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Exception ex)
            {
                timer.Stop();
                message = ex.Message;
                ArchitecturalDiagnostics.Log(nameof(CmdWallFloorFinishes), doc, "create-room-finish-floors", 0, 0, 1, timer.Elapsed, ex.GetType().FullName);
                TaskDialog.Show("KhimArchitectural Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
