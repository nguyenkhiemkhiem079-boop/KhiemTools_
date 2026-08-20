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
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.ViewportAlign.Commands
{
    /// <summary>
    /// Command: Đồng bộ và Căn chỉnh vị trí Viewport, View Titles và Bảng thống kê giữa các Sheet.
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
                Viewport preSelectedVp = null;

                // 1. Kiểm tra xem người dùng đã chọn sẵn Viewport trên Sheet chưa
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 1)
                {
                    preSelectedVp = doc.GetElement(selectedIds.First()) as Viewport;
                }

                // 2. Mở form tương tác chuyên nghiệp (tự động load viewport mẫu nếu có, hoặc cho phép pick trên form)
                var form = new AlignViewportForm(uidoc, preSelectedVp);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                Viewport sourceVp = form.SourceViewport;
                var targetViews = form.SelectedTargetViews;
                ArrangeMode mode = form.SelectedArrangeMode;

                if (sourceVp == null || !targetViews.Any())
                {
                    return Result.Cancelled;
                }

                // 3. Tiến hành căn chỉnh trên từng View/Schedule
                int viewportCount = 0;
                int scheduleCount = 0;
                int failedCount = 0;

                using (var tg = new TransactionGroup(doc, "Arrange Views & Titles Across Sheets"))
                {
                    tg.Start();

                    foreach (var targetItem in targetViews)
                    {
                        if (targetItem.IsSchedule)
                        {
                            // Bảng Schedule
                            ScheduleSheetInstance targetSched = doc.GetElement(targetItem.ViewportOrScheduleId) as ScheduleSheetInstance;
                            if (targetSched != null)
                            {
                                using (var tx = new Transaction(doc, $"Align Schedule {targetItem.ViewName}"))
                                {
                                    tx.Start();
                                    try
                                    {
                                        // Tìm schedule mẫu trên source sheet
                                        var sourceSchedules = new FilteredElementCollector(doc, sourceVp.SheetId)
                                            .OfClass(typeof(ScheduleSheetInstance))
                                            .Cast<ScheduleSheetInstance>()
                                            .ToList();

                                        var matchSource = sourceSchedules.FirstOrDefault(s => s.ScheduleId == targetSched.ScheduleId)
                                                          ?? sourceSchedules.FirstOrDefault();

                                        if (matchSource != null && ViewportAlignService.AlignSchedule(doc, targetSched, matchSource))
                                        {
                                            scheduleCount++;
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
                        }
                        else
                        {
                            // Viewport
                            Viewport targetVp = doc.GetElement(targetItem.ViewportOrScheduleId) as Viewport;
                            if (targetVp != null)
                            {
                                using (var tx = new Transaction(doc, $"Arrange Viewport {targetItem.ViewName}"))
                                {
                                    tx.Start();
                                    try
                                    {
                                        if (ViewportAlignService.AlignViewport(doc, targetVp, sourceVp, mode))
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
                        }
                    }

                    tg.Assimilate();
                }

                // Làm mới giao diện Active View
                uidoc.RefreshActiveView();

                string msgSummary = LanguageManager.IsEnglish
                    ? $"Alignment Completed!\n\n" +
                      $"• Viewports successfully aligned: {viewportCount}\n" +
                      $"• Schedules successfully aligned: {scheduleCount}\n" +
                      $"• Arrange Mode: {mode}\n" +
                      (failedCount > 0 ? $"• Errors: {failedCount}\n" : "")
                    : $"Đã hoàn tất căn chỉnh vị trí Viewport & Tiêu đề bản vẽ!\n\n" +
                      $"• Số Viewport căn chỉnh thành công: {viewportCount}\n" +
                      $"• Số Bảng Schedule căn chỉnh thành công: {scheduleCount}\n" +
                      $"• Chế độ căn chỉnh: {mode}\n" +
                      (failedCount > 0 ? $"• Lỗi: {failedCount}\n" : "");

                TaskDialog.Show("Khim Tools — Arrange Views & Title", msgSummary);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools — Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
