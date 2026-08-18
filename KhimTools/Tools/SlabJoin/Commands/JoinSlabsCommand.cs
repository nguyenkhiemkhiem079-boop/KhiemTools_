using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.SlabJoin.Interfaces;
using KhimTools.SlabJoin.Models;
using KhimTools.SlabJoin.Services;
using KhimTools.SlabJoin.Utilities;

namespace KhimTools.SlabJoin.Commands
{
    /// <summary>
    /// Command "Join Slabs" — hiện dialog chọn chế độ (Join/Unjoin × Active View/Entire Model)
    /// rồi xử lý tất cả sàn trong scope đó.
    ///
    /// Logic giống Python pyRevit reference:
    ///   • Lấy TẤT CẢ Floor (không filter Structural riêng)
    ///   • Kiểm tra BB chạm nhau
    ///   • Join: sàn dày hơn = primary, unjoin trước nếu đã joined, thử 2 thứ tự
    ///   • Unjoin: chỉ unjoin nếu đang joined
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class JoinSlabsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ILoggingService logger = new LoggingService();

            try
            {
                UIApplication uiApp = commandData.Application;
                Document doc = uiApp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    TaskDialog.Show("Join / Unjoin Floors", "No active document was found.");
                    return Result.Cancelled;
                }

                // ── 1. Hỏi user chọn chế độ ──────────────────────────────────
                var dlg = new TaskDialog("Join / Unjoin Floors")
                {
                    MainInstruction = "Chọn chế độ xử lý sàn (Floor Geometry)",
                    CommonButtons = TaskDialogCommonButtons.Close,
                    DefaultButton  = TaskDialogResult.Close
                };
                dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "JOIN — Active View",   "Nối sàn trong view hiện tại");
                dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "JOIN — Entire Model",  "Nối sàn toàn bộ model");
                dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "UNJOIN — Active View", "Bỏ nối sàn trong view hiện tại");
                dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "UNJOIN — Entire Model","Bỏ nối sàn toàn bộ model");

                TaskDialogResult choice = dlg.Show();

                bool doJoin;
                bool viewOnly;

                switch (choice)
                {
                    case TaskDialogResult.CommandLink1: doJoin = true;  viewOnly = true;  break;
                    case TaskDialogResult.CommandLink2: doJoin = true;  viewOnly = false; break;
                    case TaskDialogResult.CommandLink3: doJoin = false; viewOnly = true;  break;
                    case TaskDialogResult.CommandLink4: doJoin = false; viewOnly = false; break;
                    default: return Result.Cancelled; // Close hoặc X
                }

                string modeLabel = (doJoin ? "JOIN" : "UNJOIN") + (viewOnly ? " — Active View" : " — Entire Model");
                logger.LogInfo($"=== {modeLabel} started ===");

                // ── 2. Thu thập sàn ────────────────────────────────────────────
                var sw = Stopwatch.StartNew();
                var scanner = new SlabScannerService();
                var floors  = scanner.GetFloors(doc, viewOnly, out List<SkippedElementInfo> skipped);

                logger.LogInfo($"Floors found: {floors.Count}, skipped: {skipped.Count}");
                foreach (var s in skipped)
                    logger.LogInfo($"  Skipped {s.ElementId.ToLongValue()}: {s.Reason}");

                if (floors.Count < 2)
                {
                    TaskDialog.Show("Join / Unjoin Floors",
                        $"Tìm thấy {floors.Count} sàn trong scope '{modeLabel}'.\n" +
                        "Cần ít nhất 2 sàn để xử lý.");
                    return Result.Succeeded;
                }

                // ── 3. Tìm cặp có BB chạm nhau ─────────────────────────────────
                var spatial = new SpatialIndexService();
                var pairs   = spatial.FindCandidatePairs(doc, floors);
                logger.LogInfo($"Candidate pairs: {pairs.Count}");

                // ── 4. Thực hiện join/unjoin theo batch (Chống đơ/văng Revit) ────
                var joinService = new SlabJoinService();
                var results = new List<JoinPairResult>();

                string txGroup = doJoin ? "Join Floors Batch" : "Unjoin Floors Batch";
                int batchSize = 50;
                int totalPairs = pairs.Count;

                for (int i = 0; i < totalPairs; i += batchSize)
                {
                    var chunk = pairs.Skip(i).Take(batchSize).ToList();
                    using (var tx = new Transaction(doc, $"{txGroup} ({i + 1}-{Math.Min(i + batchSize, totalPairs)})"))
                    {
                        tx.Start();
                        FailureHandlingOptions failOptions = tx.GetFailureHandlingOptions();
                        failOptions.SetFailuresPreprocessor(new SwallowWarningsPreprocessor());
                        tx.SetFailureHandlingOptions(failOptions);

                        var batchResults = doJoin
                            ? joinService.JoinSlabs(doc, chunk)
                            : joinService.UnjoinSlabs(doc, chunk);

                        if (batchResults != null)
                            results.AddRange(batchResults);

                        tx.Commit();
                    }
                }

                sw.Stop();

                // ── 5. Tổng kết ────────────────────────────────────────────────
                int success = results.Count(r => r.Success);
                int already = results.Count(r => !r.Success && !r.IsError);
                int failed  = results.Count(r => r.IsError);

                var summary = new OperationSummary
                {
                    OperationType              = doJoin ? OperationType.Join : OperationType.Unjoin,
                    TotalStructuralFloorsScanned = floors.Count,
                    CandidatePairsFound        = pairs.Count,
                    ElapsedTime                = sw.Elapsed
                };
                summary.SkippedElements.AddRange(skipped);
                summary.ProcessedPairs.AddRange(results);
                logger.WriteSummary(summary);

                TaskDialog.Show($"{(doJoin ? "Join" : "Unjoin")} Floors — Hoàn tất",
                    $"Mode          : {modeLabel}\n" +
                    $"Sàn tìm thấy  : {floors.Count}\n" +
                    $"Cặp ứng viên  : {pairs.Count}\n" +
                    $"Thành công     : {success}\n" +
                    $"Đã xử lý trước: {already}\n" +
                    $"Lỗi            : {failed}\n" +
                    $"Thời gian      : {sw.Elapsed.TotalSeconds:F2} s");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                logger.LogError("Unhandled exception in JoinSlabsCommand", ex);
                message = ex.Message;
                TaskDialog.Show("Join / Unjoin Floors — Error", $"Lỗi không mong đợi:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
