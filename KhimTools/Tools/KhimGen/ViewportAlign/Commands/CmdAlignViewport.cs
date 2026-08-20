using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.ViewportAlign.Forms;
using KhimTools.ViewportAlign.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using View = Autodesk.Revit.DB.View;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.ViewportAlign.Commands
{
    /// <summary>
    /// Command: Đồng bộ và Căn chỉnh vị trí Viewport và Bảng Schedule trên nhiều Sheet.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignViewport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Khim Tools", "Không tìm thấy tài liệu Revit đang mở.");
                return Result.Cancelled;
            }

            try
            {
                Viewport vpSource = null;

                // 1. Kiểm tra xem người dùng đã chọn sẵn Viewport trên Sheet chưa
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 1)
                {
                    vpSource = doc.GetElement(selectedIds.First()) as Viewport;
                }

                // 2. Nếu chưa chọn, yêu cầu pick Viewport nguồn
                if (vpSource == null)
                {
                    try
                    {
                        Reference pickedRef = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new ViewportSelectionFilter(),
                            LanguageManager.IsEnglish
                                ? "Select source Viewport on Sheet to use as alignment reference"
                                : "Chọn Viewport nguồn trên Sheet để lấy vị trí mẫu");

                        if (pickedRef != null)
                        {
                            vpSource = doc.GetElement(pickedRef) as Viewport;
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }
                }

                if (vpSource == null)
                {
                    TaskDialog.Show("Khim Tools — Align Viewport",
                        LanguageManager.IsEnglish ? "No valid source viewport selected." : "Chưa chọn được Viewport nguồn hợp lệ.");
                    return Result.Cancelled;
                }

                ViewSheet sourceSheet = doc.GetElement(vpSource.SheetId) as ViewSheet;

                // 3. Mở giao diện chọn Sheet mục tiêu & Tùy chọn đối tượng
                var form = new AlignViewportForm(doc, vpSource);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                var targetSheets = form.SelectedTargetSheets;
                var alignOptions = form.AlignOptions;

                if (!targetSheets.Any())
                {
                    return Result.Cancelled;
                }

                // 4. Tiến hành căn chỉnh trên từng Sheet
                int viewportCount = 0;
                int scheduleCount = 0;
                int failedCount = 0;

                using (var tg = new TransactionGroup(doc, "Align Viewports & Schedules Across Sheets"))
                {
                    tg.Start();

                    foreach (ViewSheet sheet in targetSheets)
                    {
                        // A. Căn chỉnh Viewports
                        var vpIds = sheet.GetAllViewports();
                        foreach (ElementId vid in vpIds)
                        {
                            if (vid == vpSource.Id) continue;

                            Viewport vpTarget = doc.GetElement(vid) as Viewport;
                            if (vpTarget == null) continue;

                            View targetView = doc.GetElement(vpTarget.ViewId) as View;
                            if (!ViewportAlignService.ShouldAlignViewport(targetView, alignOptions))
                            {
                                continue;
                            }

                            using (var tx = new Transaction(doc, $"Move Viewport {targetView?.Name}"))
                            {
                                tx.Start();
                                try
                                {
                                    if (ViewportAlignService.MoveViewportToSource(doc, vpTarget, vpSource))
                                    {
                                        viewportCount++;
                                    }
                                    tx.Commit();
                                }
                                catch
                                {
                                    tx.RollBack();
                                    failedCount++;
                                }
                            }
                        }

                        // B. Căn chỉnh Bảng thống kê (Schedules) nếu được tick chọn
                        if (alignOptions.AlignSchedules && sourceSheet != null)
                        {
                            using (var tx = new Transaction(doc, $"Move Schedules on Sheet {sheet.SheetNumber}"))
                            {
                                tx.Start();
                                try
                                {
                                    scheduleCount += ViewportAlignService.AlignSchedules(doc, sheet, sourceSheet);
                                    tx.Commit();
                                }
                                catch
                                {
                                    tx.RollBack();
                                }
                            }
                        }
                    }

                    tg.Assimilate();
                }

                // Làm mới giao diện Active View
                uidoc.RefreshActiveView();

                string msgSummary = LanguageManager.IsEnglish
                    ? $"🎉 Alignment Completed!\n\n" +
                      $"• Viewports successfully aligned: {viewportCount}\n" +
                      $"• Schedules successfully aligned: {scheduleCount}\n" +
                      $"• Target Sheets processed: {targetSheets.Count}\n" +
                      (failedCount > 0 ? $"• Errors: {failedCount}\n" : "")
                    : $"🎉 Đã hoàn tất đồng bộ vị trí Viewport & Schedule!\n\n" +
                      $"• Số Viewport căn chỉnh thành công: {viewportCount}\n" +
                      $"• Số Bảng Schedule căn chỉnh thành công: {scheduleCount}\n" +
                      $"• Số Sheet mục tiêu đã xử lý: {targetSheets.Count}\n" +
                      (failedCount > 0 ? $"• Lỗi: {failedCount}\n" : "");

                TaskDialog.Show("Khim Tools — Align Viewport", msgSummary);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools — Error", ex.Message);
                return Result.Failed;
            }
        }

        private class ViewportSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Viewport;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
