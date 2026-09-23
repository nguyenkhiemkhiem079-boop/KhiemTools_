using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core.Logging;
using KhimTools.Core.Revit;
using KhimTools.Core.Workflow;
using KhimTools.SlabStep.Models;

namespace KhimTools.SlabStep.Services
{
    public static class SlabStepService
    {
        public static double InternalToMillimetres(double value) => RevitUnitService.FeetToMillimetres(value);
        public static double MillimetresToInternal(double value) => RevitUnitService.MillimetresToFeet(value);

        /// <summary>
        /// Nạp Family từ file RFA bên ngoài vào dự án
        /// </summary>
        public static Family LoadStepFamily(Document doc, string rfaPath)
        {
            return KhimTools.Core.Family.FamilyManager.LoadFamilySafely(doc, rfaPath);
        }

        /// <summary>
        /// Quét và lấy toàn bộ các Family Symbol liên quan đến nách sàn/giật cấp
        /// </summary>
        public static List<FamilySymbol> GetLoadedStepSymbols(Document doc)
        {
            var list = new List<FamilySymbol>();
            if (doc == null) return list;

            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel) // các family giật cấp thường thuộc Generic Model
                .Cast<FamilySymbol>()
                .OrderBy(s => s.Family.Name)
                .ThenBy(s => s.Name)
                .ToList();

            list.AddRange(symbols);
            return list;
        }

        /// <summary>
        /// Lấy toàn bộ tham số có kiểu là Double/Length để người dùng chọn map.
        /// Chỉ đọc từ FamilySymbol.Parameters — KHÔNG gọi EditFamily() vì sẽ gây lock UI
        /// và conflict với Revit selection mode.
        /// </summary>
        public static List<string> GetDoubleParameters(FamilySymbol symbol)
        {
            var list = new List<string>();
            if (symbol == null) return list;

            // Đọc tham số trực tiếp từ FamilySymbol (bao gồm cả type params và instance params)
            foreach (Parameter p in symbol.Parameters)
            {
                if (p.StorageType == StorageType.Double && p.Definition != null)
                {
                    list.Add(p.Definition.Name);
                }
            }

            // KHÔNG gọi EditFamily() — EditFamily() yêu cầu transaction và gây lock Revit UI,
            // làm người dùng không thể thực hiện selection sau đó.

            return list.Distinct().OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Lấy toàn bộ biên dạng đường lưới 2D của Sàn dựa vào Sketch hoặc Geometry
        /// </summary>
        public static List<Curve> GetFloorBoundaryCurves(Document doc, Floor floor)
        {
            var list = new List<Curve>();
            if (floor == null) return list;

            // 1. Thử lấy qua SketchId (Revit 2022+)
            try
            {
                var sketchId = floor.SketchId;
                if (sketchId != ElementId.InvalidElementId && doc.GetElement(sketchId) is Sketch sketch)
                {
                    foreach (CurveLoop loop in sketch.Profile)
                    {
                        foreach (Curve c in loop)
                        {
                            list.Add(c);
                        }
                    }
                    if (list.Any()) return list;
                }
            }
            catch (Exception ex) { KToolsLog.Current.Exception("SlabStep.FloorSketch", ex, "SKETCH_READ"); }

            // 2. Fallback: Lấy qua Geometry Solid
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            var geomElem = floor.get_Geometry(opt);
            if (geomElem != null)
            {
                foreach (var geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Volume > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            // Tìm mặt trên cùng (Top Face) có normal hướng lên Z
                            if (face.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(XYZ.BasisZ))
                            {
                                foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
                                {
                                    foreach (Curve c in loop)
                                    {
                                        list.Add(c);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return list;
        }

        public static List<SharedBoundarySegment> FindSharedBoundaries(Floor high, Floor low, SharedBoundaryOptions options)
        {
            var result = new List<SharedBoundarySegment>();
            if (high == null || low == null) return result;
            double tol = MillimetresToInternal(options?.GeometryToleranceMm ?? 2);
            double min = MillimetresToInternal(options?.MinimumLengthMm ?? 100);
            var highLines = GetFloorBoundaryCurves(high.Document, high).OfType<Line>().ToList();
            var lowLines = GetFloorBoundaryCurves(low.Document, low).OfType<Line>().ToList();
            foreach (var h in highLines)
            foreach (var l in lowLines)
            {
                var a = h.GetEndPoint(0); var b = h.GetEndPoint(1);
                var c = l.GetEndPoint(0); var d = l.GetEndPoint(1);
                var hv = new XYZ(b.X-a.X,b.Y-a.Y,0); var lv = new XYZ(d.X-c.X,d.Y-c.Y,0);
                if (hv.GetLength() < tol || lv.GetLength() < tol) continue;
                hv = hv.Normalize(); lv = lv.Normalize();
                if (Math.Abs(hv.X*lv.Y-hv.Y*lv.X) > 1e-6 && Math.Abs(hv.X*lv.Y-hv.Y*lv.X) > tol) continue;
                if (Math.Abs((c-a).X*hv.Y-(c-a).Y*hv.X) > tol) continue;
                double h0 = 0, h1 = h.Distance(a); double l0 = (c-a).DotProduct(hv), l1 = (d-a).DotProduct(hv);
                double start = Math.Max(h0, Math.Min(l0,l1)); double end = Math.Min(h1, Math.Max(l0,l1));
                if (end-start < min) continue;
                var p0 = a + hv*start; var p1 = a + hv*end;
                result.Add(new SharedBoundarySegment { Curve = Line.CreateBound(p0,p1) });
            }
            return result.GroupBy(segment => BoundaryKey(segment.Curve), StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(segment => BoundaryKey(segment.Curve), StringComparer.Ordinal).ToList();
        }

        public static string BoundaryFingerprint(IEnumerable<Curve> curves)
        {
            return WorkflowFingerprint.Compute((curves ?? Enumerable.Empty<Curve>()).Where(curve => curve != null)
                .Select(BoundaryKey).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal));
        }

        private static string BoundaryKey(Curve curve)
        {
            XYZ first = curve.GetEndPoint(0), second = curve.GetEndPoint(1);
            string a = PointKey(first), b = PointKey(second);
            if (string.CompareOrdinal(a, b) > 0) { string swap = a; a = b; b = swap; }
            return a + "/" + b + "/" + curve.Length.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string PointKey(XYZ point)
        {
            return point.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   point.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                   point.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Tự động tìm kiếm các đoạn cạnh tiếp xúc gần nhau giữa Sàn Cao và Sàn Thấp
        /// </summary>
        public static List<Curve> AutoDetectBoundary(Document doc, Floor floorHigh, Floor floorLow, double toleranceMm)
        {
            var result = new List<Curve>();
            if (floorHigh == null || floorLow == null) return result;

            var curvesHigh = GetFloorBoundaryCurves(doc, floorHigh);
            var curvesLow = GetFloorBoundaryCurves(doc, floorLow);

            double toleranceFeet = RevitUnitService.MillimetresToFeet(toleranceMm);

            foreach (var ch in curvesHigh)
            {
                // Chiếu phẳng ch xuống Z=0 để so sánh 2D
                XYZ startH = new XYZ(ch.GetEndPoint(0).X, ch.GetEndPoint(0).Y, 0);
                XYZ endH = new XYZ(ch.GetEndPoint(1).X, ch.GetEndPoint(1).Y, 0);
                Line lineH = Line.CreateBound(startH, endH);

                foreach (var cl in curvesLow)
                {
                    XYZ startL = new XYZ(cl.GetEndPoint(0).X, cl.GetEndPoint(0).Y, 0);
                    XYZ endL = new XYZ(cl.GetEndPoint(1).X, cl.GetEndPoint(1).Y, 0);
                    Line lineL = Line.CreateBound(startL, endL);

                    // Tính khoảng cách giữa 2 đoạn đường thẳng phẳng 2D
                    double dist = GetDistanceBetweenSegments2D(lineH, lineL);
                    if (dist <= toleranceFeet)
                    {
                        // Kiểm tra xem đoạn ch đã được thêm chưa để tránh trùng lặp
                        if (!result.Any(r => IsDuplicateCurve(r, ch)))
                        {
                            result.Add(ch);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Thực thi chèn nách sàn giật cấp dọc theo đường dẫn và gán tham số thủ công
        /// </summary>
        public static FamilyInstance GenerateSlabStep(Document doc, Curve boundaryCurve, FamilySymbol symbol, SlabStepSettings settings, double heightMm, double highThickMm, double lowThickMm, Floor floorLow = null)
        {
            SlabStepExecutionResult result = GenerateSlabStepWithResult(doc, boundaryCurve, symbol, settings, heightMm, highThickMm, lowThickMm, floorLow);
            return result.Status == SlabStepExecutionStatus.CREATED && result.CreatedElementIds.Count == 1
                ? doc.GetElement(result.CreatedElementIds[0]) as FamilyInstance
                : null;
        }

        public static SlabStepExecutionResult GenerateSlabStepWithResult(Document doc, Curve boundaryCurve, FamilySymbol symbol, SlabStepSettings settings, double heightMm, double highThickMm, double lowThickMm, Floor floorLow = null)
        {
            var result = new SlabStepExecutionResult();
            if (doc == null || doc.IsReadOnly || boundaryCurve == null || symbol == null || settings == null || symbol.Document != doc)
                return ValidationFailure(result, "SLAB_STEP_INVALID_INPUT", "Document, curve, symbol, or settings are unavailable or belong to another document.");
            if (!IsFinite(heightMm) || heightMm <= 0 || !IsFinite(highThickMm) || highThickMm < 0 || !IsFinite(lowThickMm) || lowThickMm < 0)
                return ValidationFailure(result, "SLAB_STEP_INVALID_DIMENSIONS", "Height must be positive; optional slab thicknesses must be finite and non-negative.");

            XYZ p1;
            XYZ p2;
            try { p1 = boundaryCurve.GetEndPoint(0); p2 = boundaryCurve.GetEndPoint(1); }
            catch (Exception ex) { return RevitFailure(result, "SLAB_STEP_INVALID_BOUNDARY", ex); }
            if (p1 == null || p2 == null || p1.DistanceTo(p2) <= 1e-9)
                return ValidationFailure(result, "SLAB_STEP_INVALID_BOUNDARY", "Boundary curve must have two distinct endpoints.");

            // Lấy Level của View hiện hành để làm Host chính
            Level level = doc.ActiveView == null ? null : doc.ActiveView.GenLevel;
            if (level == null)
            {
                level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(candidate => candidate.Elevation)
                    .ThenBy(candidate => candidate.Id.IntegerValue)
                    .FirstOrDefault();
            }
            if (level == null) return ValidationFailure(result, "SLAB_STEP_LEVEL_MISSING", "No valid placement Level is available.");

            // Quy đổi đơn vị mm sang feet (internal Revit units)
            double heightDiff = RevitUnitService.MillimetresToFeet(heightMm);
            double thickHigh = RevitUnitService.MillimetresToFeet(highThickMm);
            double thickLow = RevitUnitService.MillimetresToFeet(lowThickMm);

            // Lấy điểm đầu cuối của cạnh ranh giới
            // Đưa cao độ điểm chèn về đúng cao độ của Level
            p1 = new XYZ(p1.X, p1.Y, level.Elevation);
            p2 = new XYZ(p2.X, p2.Y, level.Elevation);

            // Xác định hướng xoay dựa vào vị trí Sàn Thấp (WC) nếu được chọn
            bool shouldSwap = false;
            if (floorLow != null)
            {
                shouldSwap = DetermineIfNeedsSwapLow(floorLow, p1, p2);
            }
            if (settings.ReverseOrientation)
            {
                shouldSwap = !shouldSwap;
            }

            XYZ startPt = shouldSwap ? p2 : p1;
            XYZ endPt = shouldSwap ? p1 : p2;

            using (var tx = new Transaction(doc, "K-TOOLS - Create Slab Step"))
            {
                if (tx.Start() != TransactionStatus.Started) return RevitFailure(result, "SLAB_STEP_TRANSACTION_START", "Creation transaction did not start.");
                try
                {
                if (!symbol.IsActive) symbol.Activate();

                // Tạo Line đặt family — bắt buộc dùng overload NewFamilyInstance(Line, ...)
                // cho line-based family. Dùng point-based insert rồi đổi LocationCurve sau
                // là anti-pattern không hoạt động với line-based families trong Revit 2022+.
                Line placementLine = Line.CreateBound(startPt, endPt);

                FamilyInstance instance;
                FamilyPlacementType placementType = symbol.Family.FamilyPlacementType;
                if (placementType == FamilyPlacementType.CurveBased)
                    instance = doc.Create.NewFamilyInstance(placementLine, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                else if (placementType == FamilyPlacementType.OneLevelBased)
                    instance = doc.Create.NewFamilyInstance((startPt + endPt) / 2.0, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                else
                    throw new InvalidOperationException("UNSUPPORTED_FAMILY_PLACEMENT: " + placementType);

                if (instance == null)
                {
                    result.RollbackResult = tx.RollBack();
                    result.RollbackVerified = result.RollbackResult == TransactionStatus.RolledBack;
                    result.Status = result.RollbackVerified ? SlabStepExecutionStatus.ROLLED_BACK : SlabStepExecutionStatus.REVIT_FAILURE;
                    result.DiagnosticCode = "SLAB_STEP_CREATION_RETURNED_NULL";
                    result.Message = "Revit did not create a Slab Step instance.";
                    return result;
                }

                // Gán tham số chiều cao giật cấp h
                if (!string.IsNullOrEmpty(settings.HeightParameterName))
                {
                    var pHeight = instance.LookupParameter(settings.HeightParameterName);
                    SetLengthParameter(pHeight, heightDiff, settings.HeightParameterName);
                }

                // Gán tham số dày sàn cao (nếu có)
                if (!string.IsNullOrEmpty(settings.HighSlabThicknessParameter) && highThickMm > 0)
                {
                    var pThickHigh = instance.LookupParameter(settings.HighSlabThicknessParameter);
                    SetLengthParameter(pThickHigh, thickHigh, settings.HighSlabThicknessParameter);
                }

                // Gán tham số dày sàn thấp (nếu có)
                if (!string.IsNullOrEmpty(settings.LowSlabThicknessParameter) && lowThickMm > 0)
                {
                    var pThickLow = instance.LookupParameter(settings.LowSlabThicknessParameter);
                    SetLengthParameter(pThickLow, thickLow, settings.LowSlabThicknessParameter);
                }

                result.TransactionResult = tx.Commit();
                if (result.TransactionResult != TransactionStatus.Committed)
                {
                    if (tx.GetStatus() == TransactionStatus.Started) { result.RollbackResult = tx.RollBack(); result.RollbackVerified = result.RollbackResult == TransactionStatus.RolledBack; }
                    return RevitFailure(result, "SLAB_STEP_TRANSACTION_COMMIT", "Creation transaction did not commit: " + result.TransactionResult);
                }
                result.CreatedElementIds.Add(instance.Id);
                result.Status = SlabStepExecutionStatus.CREATED;
                result.DiagnosticCode = "SLAB_STEP_CREATED";
                result.Message = "Slab Step was created and its transaction committed.";
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        result.RollbackResult = tx.RollBack();
                        result.RollbackVerified = result.RollbackResult == TransactionStatus.RolledBack;
                    }
                    return RevitFailure(result, "SLAB_STEP_CREATE_FAILED", ex);
                }
            }
            return result;
        }

        public static SlabStepExecutionResult GenerateSlabSteps(Document doc, IEnumerable<Curve> boundaries, FamilySymbol symbol, SlabStepSettings settings, double heightMm, double highThickMm, double lowThickMm, Floor floorLow = null)
        {
            Stopwatch timer = Stopwatch.StartNew();
            IList<Curve> curves = (boundaries ?? Enumerable.Empty<Curve>()).ToList();
            var batch = new SlabStepExecutionResult
            {
                Operation = "SlabStep.GenerateBatch",
                InputSummary = "boundaries=" + curves.Count + ";heightMm=" + heightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            if (doc == null || doc.IsReadOnly || curves.Count == 0 || symbol == null || settings == null)
                return ValidationFailure(batch, "SLAB_STEP_BATCH_INVALID_INPUT", "A writable project, at least one boundary, a symbol, and settings are required.");

            using (var group = new TransactionGroup(doc, "K-TOOLS - Create Slab Steps"))
            {
                bool started = false;
                try
                {
                    if (group.Start() != TransactionStatus.Started)
                        return RevitFailure(batch, "SLAB_STEP_BATCH_GROUP_START", "Slab Step batch TransactionGroup did not start.");
                    started = true;
                    foreach (Curve curve in curves)
                    {
                        SlabStepExecutionResult one = GenerateSlabStepWithResult(doc, curve, symbol, settings, heightMm, highThickMm, lowThickMm, floorLow);
                        foreach (ElementId id in one.CreatedElementIds) if (!batch.CreatedElementIds.Contains(id)) batch.CreatedElementIds.Add(id);
                        if (one.Status != SlabStepExecutionStatus.CREATED)
                        {
                            batch.DiagnosticCode = one.DiagnosticCode;
                            batch.Message = one.Message;
                            batch.ExceptionType = one.ExceptionType;
                            batch.FailureCount++;
                            batch.TransactionResult = one.TransactionResult;
                            batch.RollbackResult = group.RollBack();
                            batch.RollbackVerified = batch.RollbackResult == TransactionStatus.RolledBack;
                            batch.Status = batch.RollbackVerified ? SlabStepExecutionStatus.ROLLED_BACK : SlabStepExecutionStatus.REVIT_FAILURE;
                            timer.Stop(); batch.Duration = timer.Elapsed;
                            return batch;
                        }
                    }
                    batch.TransactionResult = group.Assimilate();
                    if (batch.TransactionResult != TransactionStatus.Committed)
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                        {
                            batch.RollbackResult = group.RollBack();
                            batch.RollbackVerified = batch.RollbackResult == TransactionStatus.RolledBack;
                        }
                        batch.Status = SlabStepExecutionStatus.REVIT_FAILURE;
                        batch.DiagnosticCode = "SLAB_STEP_BATCH_GROUP_COMMIT";
                        batch.Message = "Slab Step batch did not commit: " + batch.TransactionResult;
                        batch.FailureCount++;
                        started = false;
                        return batch;
                    }
                    started = false;
                    batch.Status = SlabStepExecutionStatus.CREATED;
                    batch.DiagnosticCode = "SLAB_STEP_BATCH_CREATED";
                    batch.Message = "Created " + batch.CreatedElementIds.Count + " Slab Step instance(s).";
                }
                catch (Exception ex)
                {
                    batch.ExceptionType = ex.GetType().FullName;
                    batch.DiagnosticCode = "SLAB_STEP_BATCH_FAILED";
                    batch.Message = ex.Message;
                    batch.FailureCount++;
                    KToolsLog.Current.Exception("SlabStep.GenerateBatch", ex, batch.DiagnosticCode);
                    if (started && group.GetStatus() == TransactionStatus.Started)
                    {
                        batch.RollbackResult = group.RollBack();
                        batch.RollbackVerified = batch.RollbackResult == TransactionStatus.RolledBack;
                    }
                    batch.Status = batch.RollbackVerified ? SlabStepExecutionStatus.ROLLED_BACK : SlabStepExecutionStatus.REVIT_FAILURE;
                }
            }
            timer.Stop(); batch.Duration = timer.Elapsed;
            return batch;
        }

        private static void SetLengthParameter(Parameter parameter, double value, string name)
        {
            if (parameter == null) throw new InvalidOperationException("SLAB_STEP_PARAMETER_MISSING: " + name);
            if (parameter.IsReadOnly) throw new InvalidOperationException("SLAB_STEP_PARAMETER_READ_ONLY: " + name);
            if (parameter.StorageType != StorageType.Double) throw new InvalidOperationException("SLAB_STEP_PARAMETER_STORAGE_UNSUPPORTED: " + name);
            parameter.Set(value);
        }

        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static SlabStepExecutionResult ValidationFailure(SlabStepExecutionResult result, string code, string message)
        { result.Status = SlabStepExecutionStatus.VALIDATION_FAILURE; result.DiagnosticCode = code; result.Message = message; return result; }
        private static SlabStepExecutionResult RevitFailure(SlabStepExecutionResult result, string code, string message)
        { result.Status = SlabStepExecutionStatus.REVIT_FAILURE; result.DiagnosticCode = code; result.Message = message; return result; }
        private static SlabStepExecutionResult RevitFailure(SlabStepExecutionResult result, string code, Exception exception)
        { result.ExceptionType = exception == null ? string.Empty : exception.GetType().FullName; result.Message = exception == null ? "Unknown Revit failure." : exception.Message; KToolsLog.Current.Exception("SlabStep", exception, code); return RevitFailure(result, code, result.Message); }

        #region PRIVATE GEOMETRIC HELPERS

        public static double GetFloorTopElevation(Floor floor)
        {
            var pOffset = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = (pOffset != null && pOffset.HasValue) ? pOffset.AsDouble() : 0.0;

            var level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElevation = (level != null) ? level.Elevation : 0.0;

            return levelElevation + offset;
        }

        private static bool DetermineIfNeedsSwap(Floor floorHigh, XYZ p1, XYZ p2)
        {
            // Vector chỉ hướng đoạn ranh giới phẳng 2D
            XYZ dir = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0).Normalize();
            XYZ normal = XYZ.BasisZ;
            
            // Hướng chỉ sang bên trái của đường đi (Cross Product)
            XYZ sideVec = dir.CrossProduct(normal).Normalize();

            // Lấy điểm test cách ranh giới 1 foot về phía bên trái
            XYZ mid = (p1 + p2) / 2.0;
            XYZ testPt = mid + sideVec * 1.0;

            // Kiểm tra xem testPt có nằm trong sàn cao không
            bool isInsideHigh = IsPointInsideFloor2D(floorHigh, testPt);

            // Mặc định nách sàn được thiết kế có mặt cao bên tay trái
            // Nếu sàn cao nằm bên tay phải (không nằm bên tay trái), ta cần đảo chiều
            return !isInsideHigh;
        }

        private static bool DetermineIfNeedsSwapLow(Floor floorLow, XYZ p1, XYZ p2)
        {
            // Vector chỉ hướng đoạn ranh giới phẳng 2D
            XYZ dir = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0).Normalize();
            XYZ normal = XYZ.BasisZ;
            
            // Hướng chỉ sang bên trái của đường đi (Cross Product)
            XYZ sideVec = dir.CrossProduct(normal).Normalize();

            // Lấy điểm test cách ranh giới 1 foot về phía bên trái
            XYZ mid = (p1 + p2) / 2.0;
            XYZ testPt = mid + sideVec * 1.0;

            // Kiểm tra xem testPt có nằm trong sàn thấp không
            bool isInsideLow = IsPointInsideFloor2D(floorLow, testPt);

            // Mặc định nách sàn được thiết kế có mặt cao bên tay trái (phía sàn cao) và mặt thấp bên tay phải (phía sàn thấp)
            // Nếu sàn thấp nằm bên tay trái, ta cần đảo chiều để sàn thấp chuyển sang tay phải
            return isInsideLow;
        }

        public static bool IsPointInsideFloor2D(Floor floor, XYZ pt)
        {
            var doc = floor.Document;
            var curves = GetFloorBoundaryCurves(doc, floor);
            if (!curves.Any()) return false;

            // Giải thuật Ray-Casting kiểm tra điểm trong đa giác phẳng Z=0
            int intersections = 0;
            XYZ rayEnd = new XYZ(pt.X + 10000.0, pt.Y + 1.234, 0); // Ray ngẫu nhiên nằm ngang dài 10000 ft

            foreach (var c in curves)
            {
                XYZ p1 = new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 0);
                XYZ p2 = new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 0);

                if (IsLineSegmentIntersection2D(pt, rayEnd, p1, p2))
                {
                    intersections++;
                }
            }

            return (intersections % 2 != 0);
        }

        private static bool IsLineSegmentIntersection2D(XYZ a1, XYZ a2, XYZ b1, XYZ b2)
        {
            double d = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
            if (Math.Abs(d) < 1e-9) return false; // Song song

            double u = ((b1.X - a1.X) * (b2.Y - b1.Y) - (b1.Y - a1.Y) * (b2.X - b1.X)) / d;
            double v = ((b1.X - a1.X) * (a2.Y - a1.Y) - (b1.Y - a1.Y) * (a2.X - a1.X)) / d;

            return (u >= 0 && u <= 1 && v >= 0 && v <= 1);
        }

        private static double GetDistanceBetweenSegments2D(Line l1, Line l2)
        {
            // Tính toán khoảng cách gần nhất giữa 2 phân đoạn thẳng phẳng 2D
            double d1 = l1.Distance(l2.GetEndPoint(0));
            double d2 = l1.Distance(l2.GetEndPoint(1));
            double d3 = l2.Distance(l1.GetEndPoint(0));
            double d4 = l2.Distance(l1.GetEndPoint(1));

            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        private static bool IsDuplicateCurve(Curve c1, Curve c2)
        {
            XYZ s1 = c1.GetEndPoint(0);
            XYZ e1 = c1.GetEndPoint(1);
            XYZ s2 = c2.GetEndPoint(0);
            XYZ e2 = c2.GetEndPoint(1);

            return (s1.IsAlmostEqualTo(s2) && e1.IsAlmostEqualTo(e2)) ||
                   (s1.IsAlmostEqualTo(e2) && e1.IsAlmostEqualTo(s2));
        }

        #endregion
    }
}
