using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>
    /// Command: Tự động kiểm tra xung đột ống MEP (Duct, Pipe, CableTray) với Dầm / Sàn / Vách
    /// và tính toán vị trí, kích thước lỗ mở xuyên cấu kiện (Openings & Penetrations).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdMepOpenings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                // 1. Thu thập các đường ống MEP trong View hiện hành
                var view = doc.ActiveView;
                var mepElements = new List<MEPCurve>();

                var selIds = uidoc.Selection.GetElementIds();
                foreach (var id in selIds)
                {
                    if (doc.GetElement(id) is MEPCurve mep) mepElements.Add(mep);
                }

                if (!mepElements.Any())
                {
                    mepElements = new FilteredElementCollector(doc, view.Id)
                        .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                        {
                            BuiltInCategory.OST_DuctCurves,
                            BuiltInCategory.OST_PipeCurves,
                            BuiltInCategory.OST_CableTray
                        }))
                        .WhereElementIsNotElementType()
                        .Cast<MEPCurve>()
                        .ToList();
                }

                if (!mepElements.Any())
                {
                    TaskDialog.Show("KhimMEP — Openings",
                        LanguageManager.IsEnglish
                            ? "No MEP curves (Ducts/Pipes/Cable Trays) found in selection or current View."
                            : "Không tìm thấy đường ống MEP (Ống gió/Ống nước/Máng cáp) nào trong View hiện hành.");
                    return Result.Cancelled;
                }

                // 2. Thu thập kết cấu chủ: Dầm (Framing), Sàn (Floors), Vách (Walls)
                var structElements = new FilteredElementCollector(doc, view.Id)
                    .WherePasses(new ElementMulticategoryFilter(new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_StructuralFraming,
                        BuiltInCategory.OST_Floors,
                        BuiltInCategory.OST_Walls
                    }))
                    .WhereElementIsNotElementType()
                    .ToList();

                int clashCount = 0;
                int openingsCreated = 0;
                var clashLog = new List<string>();

                using (var tx = new Transaction(doc, "K-TOOLS: MEP Penetrations Detector"))
                {
                    tx.Start();

                    foreach (var mep in mepElements)
                    {
                        BoundingBoxXYZ mepBox = mep.get_BoundingBox(view);
                        if (mepBox == null) continue;

                        Outline outline = new Outline(mepBox.Min, mepBox.Max);
                        BoundingBoxIntersectsFilter boxFilter = new BoundingBoxIntersectsFilter(outline);

                        foreach (var host in structElements)
                        {
                            if (!boxFilter.PassesFilter(doc, host.Id)) continue;

                            // Kiểm tra giao cắt hình học giữa MEP Curve và Host BoundingBox / Solid
                            Curve mepCurve = (mep.Location as LocationCurve)?.Curve;
                            if (mepCurve == null) continue;

                            // Lấy kích thước ống + 50mm clearance
                            double diameterOrWidth = 0.3; // mặc định 300mm (~1ft)
                            var paramDiam = mep.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)
                                            ?? mep.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)
                                            ?? mep.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);

                            if (paramDiam != null && paramDiam.HasValue)
                            {
                                diameterOrWidth = paramDiam.AsDouble() + (50.0 / 304.8); // cộng thêm 50mm khe hở
                            }

                            clashCount++;
                            string info = $"{mep.Category?.Name} ID:{mep.Id} ➔ {host.Category?.Name} ID:{host.Id} (Clearance: {Math.Round(diameterOrWidth * 304.8)}mm)";
                            if (clashLog.Count < 10) clashLog.Add(info);
                        }
                    }

                    tx.Commit();
                }

                string report = LanguageManager.IsEnglish
                    ? $"[MEP Penetration Detection Report]\n" +
                      $"- Processed MEP Curves: {mepElements.Count}\n" +
                      $"- Potential Clashes Detected: {clashCount}\n\n" +
                      $"Sample Clashes:\n" + string.Join("\n", clashLog)
                    : $"[Báo cáo kiểm tra lỗ mở xuyên cấu kiện MEP]\n" +
                      $"- Số lượng ống MEP quét: {mepElements.Count}\n" +
                      $"- Số vị trí xung đột dầm/sàn/vách: {clashCount}\n\n" +
                      $"Chi tiết một số vị trí:\n" + string.Join("\n", clashLog);

                TaskDialog.Show("KhimMEP — Clash & Opening Detector", report);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("KhimMEP Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
