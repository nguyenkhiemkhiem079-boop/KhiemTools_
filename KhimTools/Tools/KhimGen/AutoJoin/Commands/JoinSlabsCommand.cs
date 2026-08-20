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

                // ── 2. Thiết lập quy tắc nối cấu kiện kết cấu ──────────────────
                var sw = Stopwatch.StartNew();
                var joinService = new ElementJoinService();
                ScopeMode scope = viewOnly ? ScopeMode.CurrentView : ScopeMode.AllModel;

                var rules = new List<CategoryMatchRule>
                {
                    new CategoryMatchRule(BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFraming),
                    new CategoryMatchRule(BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralColumns),
                    new CategoryMatchRule(BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls),
                    new CategoryMatchRule(BuiltInCategory.OST_Floors, BuiltInCategory.OST_Floors),
                    new CategoryMatchRule(BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralColumns),
                    new CategoryMatchRule(BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralFraming)
                };

                // ── 3. Thực thi Join/Unjoin đa cấu kiện ────────────────────────
                var results = doJoin
                    ? joinService.JoinByRules(doc, rules, scope, null, msg => logger.LogInfo(msg))
                    : joinService.UnjoinByRules(doc, rules, scope, null, msg => logger.LogInfo(msg));

                sw.Stop();

                // ── 4. Tổng kết ────────────────────────────────────────────────
                int success = results.Count(r => r.Success);
                int already = results.Count(r => !r.Success && !r.IsError);
                int failed  = results.Count(r => r.IsError);
                int totalPairs = results.Count;

                TaskDialog.Show($"{(doJoin ? "Join" : "Unjoin")} Geometry — Hoàn tất",
                    $"Mode          : {modeLabel}\n" +
                    $"Cặp ứng viên  : {totalPairs} (Sàn-Dầm, Sàn-Cột, Sàn-Tường, Dầm-Cột, Sàn-Sàn)\n" +
                    $"Thành công     : {success}\n" +
                    $"Đã xử lý trước: {already}\n" +
                    $"Lỗi            : {failed}\n" +
                    $"Thời gian      : {sw.Elapsed.TotalSeconds:F2} s\n\n" +
                    $"💡 Mẹo: Dùng công cụ 'Join Elements' trên Ribbon để tùy chỉnh quy tắc nối theo ý muốn.");

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
