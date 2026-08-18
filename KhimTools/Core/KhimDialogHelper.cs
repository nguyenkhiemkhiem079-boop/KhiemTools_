using System;
using System.Text;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Hiển thị các hộp thoại thông báo TaskDialog chuyên nghiệp, song ngữ Việt - Anh.
    /// Thay thế 100% các MessageBox.Show thô sơ bằng TaskDialog chuẩn giao diện Revit.
    /// </summary>
    public static class KhimDialogHelper
    {
        public static void ShowSuccess(string mainInstruction, string mainContent, string expandedContent = null)
        {
            var dialog = new TaskDialog("KHIM TOOLS")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent,
                MainIcon = TaskDialogIcon.TaskDialogIconInformation,
                CommonButtons = TaskDialogCommonButtons.Ok,
                DefaultButton = TaskDialogResult.Ok
            };

            if (!string.IsNullOrEmpty(expandedContent))
            {
                dialog.ExpandedContent = expandedContent;
            }

            dialog.Show();
        }

        public static void ShowInfo(string mainInstruction, string mainContent = "", string expandedContent = null)
        {
            ShowSuccess(mainInstruction, mainContent, expandedContent);
        }

        public static void ShowWarning(string mainInstruction, string mainContent = "")
        {
            var dialog = new TaskDialog("KHIM TOOLS")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent ?? "",
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            dialog.Show();
        }

        public static void ShowError(string mainInstruction, string mainContent = "", string details = null)
        {
            var dialog = new TaskDialog("KHIM TOOLS — Error")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent ?? "",
                MainIcon = TaskDialogIcon.TaskDialogIconError,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            if (!string.IsNullOrEmpty(details))
            {
                dialog.ExpandedContent = details;
            }

            dialog.Show();
        }

        /// <summary>
        /// Thông báo hoàn thành bố trí thép cột chuẩn chuyên nghiệp (thay thế chuỗi ghép ngoặc lộn xộn cũ).
        /// </summary>
        public static void ShowColumnRebarSuccess(int columnCount, int axisGroupCount, bool hasDrawings, bool has3DViews)
        {
            bool isEn = LanguageManager.IsEnglish;

            string mainInstruction = isEn
                ? "⚡ Column Reinforcement Completed Successfully!"
                : "⚡ Bố Trí Thép Cột Hoàn Tất Thành Công!";

            var sb = new StringBuilder();
            if (isEn)
            {
                sb.AppendLine($"• Total columns processed: {columnCount} columns");
                sb.AppendLine($"• Multi-story axis groups: {axisGroupCount} stack(s)");
                if (hasDrawings) sb.AppendLine("• 2D Section & Schedule Drawings: Generated");
                if (has3DViews) sb.AppendLine("• 3D Rebar Inspection Views: Created");
            }
            else
            {
                sb.AppendLine($"• Tổng số cột đã xử lý: {columnCount} cột");
                sb.AppendLine($"• Nhóm trục cột liên tầng: {axisGroupCount} nhóm trục");
                if (hasDrawings) sb.AppendLine("• Bản vẽ mặt cắt 2D & Thống kê thép: Đã khởi tạo");
                if (has3DViews) sb.AppendLine("• View 3D kiểm tra thép: Đã khởi tạo");
            }

            ShowSuccess(mainInstruction, sb.ToString());
        }

        /// <summary>
        /// Thông báo hoàn thành bố trí thép dầm.
        /// </summary>
        public static void ShowBeamRebarSuccess(int beamCount)
        {
            bool isEn = LanguageManager.IsEnglish;

            string mainInstruction = isEn
                ? "⚡ Beam Reinforcement Completed Successfully!"
                : "⚡ Bố Trí Thép Dầm Hoàn Tất Thành Công!";

            string content = isEn
                ? $"• Total structural framing beams processed: {beamCount} beams"
                : $"• Tổng số dầm kết cấu đã bố trí thép: {beamCount} dầm";

            ShowSuccess(mainInstruction, content);
        }

        /// <summary>
        /// Thông báo hoàn thành Join / Unjoin / Switch.
        /// </summary>
        public static void ShowJoinElementsSuccess(string actionName, int processedCount, int totalPairs)
        {
            bool isEn = LanguageManager.IsEnglish;

            string mainInstruction = isEn
                ? $"⚡ {actionName} Geometry Completed!"
                : $"⚡ Thao Tác {actionName} Hình Học Hoàn Tất!";

            string content = isEn
                ? $"• Elements processed: {processedCount} / {totalPairs} pairs"
                : $"• Số cặp cấu kiện đã xử lý: {processedCount} / {totalPairs} cặp";

            ShowSuccess(mainInstruction, content);
        }

        /// <summary>
        /// Hiển thị báo cáo kết quả sinh thép chi tiết (Bao gồm cảnh báo nếu có thanh thép bị lỗi).
        /// </summary>
        public static void ShowRebarGenerationReport(RebarTool.Core.RebarGenerationReport report, string subjectName, int hostCount)
        {
            if (report == null) return;
            bool isEn = LanguageManager.IsEnglish;

            if (!report.HasErrors)
            {
                string mainInstruction = isEn
                    ? $"⚡ {subjectName} Reinforcement Completed Successfully!"
                    : $"⚡ Bố Trí Thép {subjectName} Hoàn Tất Thành Công!";

                string content = isEn
                    ? $"• Successfully created {report.SuccessBarCount} rebar(s) on {hostCount} element(s)."
                    : $"• Đã tạo thành công {report.SuccessBarCount} thanh/nhóm thép trên {hostCount} cấu kiện.";

                ShowSuccess(mainInstruction, content);
            }
            else
            {
                string mainInstruction = isEn
                    ? $"⚠️ {subjectName} Reinforcement Completed with Warnings!"
                    : $"⚠️ Bố Trí Thép {subjectName} Hoàn Tất Có Cảnh Báo!";

                var sbMain = new StringBuilder();
                if (isEn)
                {
                    sbMain.AppendLine($"• Successfully created: {report.SuccessBarCount} / {report.AttemptedBarCount} rebar(s)");
                    sbMain.AppendLine($"• Failed / Skipped: {report.FailureCount} rebar(s)");
                    sbMain.AppendLine($"• Total elements processed: {hostCount}");
                    sbMain.AppendLine("\nSome rebars could not be generated due to geometric constraints or invalid host data. Click 'Show details' below to inspect.");
                }
                else
                {
                    sbMain.AppendLine($"• Số thanh/nhóm thép tạo thành công: {report.SuccessBarCount} / {report.AttemptedBarCount}");
                    sbMain.AppendLine($"• Số thanh/nhóm thép thất bại: {report.FailureCount}");
                    sbMain.AppendLine($"• Tổng số cấu kiện đã xử lý: {hostCount}");
                    sbMain.AppendLine("\nMột số thanh thép không thể khởi tạo do giới hạn hình học hoặc dữ liệu host. Bấm 'Show details' (Xem chi tiết) bên dưới để kiểm tra.");
                }

                var sbDetails = new StringBuilder();
                sbDetails.AppendLine(isEn ? "[Detailed Rebar Generation Errors]:" : "[Chi tiết các thanh thép không tạo được]:");
                foreach (var err in report.Errors)
                {
                    sbDetails.AppendLine(err.ToString());
                }

                var dialog = new TaskDialog("KHIM TOOLS — Rebar Generation Report")
                {
                    MainInstruction = mainInstruction,
                    MainContent = sbMain.ToString(),
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    CommonButtons = TaskDialogCommonButtons.Ok,
                    DefaultButton = TaskDialogResult.Ok,
                    ExpandedContent = sbDetails.ToString()
                };

                dialog.Show();
            }
        }
    }
}
