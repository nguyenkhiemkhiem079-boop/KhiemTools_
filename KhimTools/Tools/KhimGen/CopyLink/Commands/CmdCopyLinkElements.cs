using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.CopyLink.Forms;
using KhimTools.CopyLink.Services;
using KhimTools.Core;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.CopyLink.Commands
{
    /// <summary>
    /// Command: Sao chép các đối tượng (Elements/Categories) từ file Revit Link vào dự án chính.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCopyLinkElements : IExternalCommand
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
                var form = new CopyLinkElementsForm(uidoc);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                var linkInfo = form.SelectedLinkInstance;
                var selectedCategories = form.SelectedCategories;

                if (linkInfo == null || linkInfo.LinkDocument == null || !selectedCategories.Any())
                {
                    return Result.Cancelled;
                }

                // Tập hợp toàn bộ ElementIds cần copy
                var allElementIdsToCopy = new List<ElementId>();
                foreach (var cat in selectedCategories)
                {
                    allElementIdsToCopy.AddRange(cat.ElementIds);
                }

                if (!allElementIdsToCopy.Any())
                {
                    TaskDialog.Show("Khim Tools", "Không có đối tượng nào để sao chép.");
                    return Result.Cancelled;
                }

                int totalCopied = 0;
                var errorList = new List<string>();

                using (var tx = new Transaction(doc, $"Copy Elements from Link: {linkInfo.LinkDocument.Title}"))
                {
                    tx.Start();
                    try
                    {
                        var res = LinkElementCopyService.CopyElements(
                            doc,
                            linkInfo.LinkDocument,
                            linkInfo.TotalTransform,
                            allElementIdsToCopy);

                        totalCopied = res.copiedCount;
                        errorList = res.errors;

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        TaskDialog.Show("Lỗi sao chép", ex.Message);
                        return Result.Failed;
                    }
                }

                uidoc.RefreshActiveView();

                string catSummary = string.Join("\n", selectedCategories.Select(c => $"• {c.CategoryName}: {c.ElementCount} đối tượng"));
                string msg = LanguageManager.IsEnglish
                    ? $"Copied Successfully!\n\n" +
                      $"• Source Link: {linkInfo.LinkDocument.Title}\n" +
                      $"• Total Elements Copied: {totalCopied}\n\n" +
                      $"Categories processed:\n{catSummary}"
                    : $"Sao chép thành công từ Revit Link!\n\n" +
                      $"• File Link nguồn: {linkInfo.LinkDocument.Title}\n" +
                      $"• Tổng số đối tượng đã copy: {totalCopied}\n\n" +
                      $"Danh mục Category đã sao chép:\n{catSummary}";

                if (errorList.Any())
                {
                    msg += $"\n\nLưu ý một số lỗi:\n{string.Join("\n", errorList.Take(3))}";
                }

                TaskDialog.Show("Khim Tools — Copy Link Elements", msg);

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
