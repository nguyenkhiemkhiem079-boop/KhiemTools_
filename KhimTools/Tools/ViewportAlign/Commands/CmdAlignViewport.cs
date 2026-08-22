using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.ViewportAlign.Forms;
using KhimTools.ViewportAlign.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using View = Autodesk.Revit.DB.View;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.ViewportAlign.Commands
{
    /// <summary>
    /// Command: Đồng bộ và Căn chỉnh vị trí Viewport trên nhiều Sheet (Bản vẽ).
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
                TaskDialog.Show("K-TOOLS", "Không tìm thấy tài liệu Revit đang mở.");
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
                            "Chọn Viewport nguồn trên Sheet để lấy vị trí mẫu");

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
                    TaskDialog.Show("K-TOOLS — Align Viewport", "Chưa chọn được Viewport nguồn hợp lệ.");
                    return Result.Cancelled;
                }

                // 3. Mở giao diện chọn Sheet mục tiêu
                var form = new AlignViewportForm(doc, vpSource);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                var targetSheets = form.SelectedTargetSheets;
                bool skipLegends = form.SkipLegends;

                if (!targetSheets.Any())
                {
                    return Result.Cancelled;
                }

                // 4. Tiến hành căn chỉnh trên từng Sheet
                int successCount = 0;
                int skippedCount = 0;
                int failedCount = 0;

                using (var tg = new TransactionGroup(doc, "Align Viewport Across Sheets"))
                {
                    tg.Start();

                    foreach (ViewSheet sheet in targetSheets)
                    {
                        var vpIds = sheet.GetAllViewports();
                        foreach (ElementId vid in vpIds)
                        {
                            if (vid == vpSource.Id) continue;

                            Viewport vpTarget = doc.GetElement(vid) as Viewport;
                            if (vpTarget == null) continue;

                            View targetView = doc.GetElement(vpTarget.ViewId) as View;
                            if (skipLegends && ViewportAlignService.IsSkipView(targetView))
                            {
                                skippedCount++;
                                continue;
                            }

                            using (var tx = new Transaction(doc, $"Move Viewport {targetView?.Name}"))
                            {
                                tx.Start();
                                try
                                {
                                    ViewportAlignService.MoveViewportToSource(doc, vpTarget, vpSource);
                                    tx.Commit();
                                    successCount++;
                                }
                                catch
                                {
                                    tx.RollBack();
                                    failedCount++;
                                }
                            }
                        }
                    }

                    tg.Assimilate();
                }

                // Làm mới giao diện Active View
                uidoc.RefreshActiveView();

                TaskDialog.Show("K-TOOLS — Hoàn tất căn chỉnh Viewport",
                    $"🎉 Đã hoàn tất đồng bộ vị trí Viewport!\n\n" +
                    $"• Số Viewport căn chỉnh thành công: {successCount}\n" +
                    $"• Số Sheet mục tiêu: {targetSheets.Count}\n" +
                    $"• Đã bỏ qua an toàn (Legends / Schedules): {skippedCount}\n" +
                    (failedCount > 0 ? $"• Lỗi: {failedCount}\n" : ""));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("K-TOOLS — Lỗi Align Viewport", $"Lỗi không mong đợi:\n{ex.Message}");
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
