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
    /// Command: ─Éß╗ông bß╗Ö v├á C─ân chß╗ënh vß╗ï tr├¡ Viewport, View Titles v├á Bß║úng thß╗æng k├¬ giß╗»a c├íc Sheet.
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
                TaskDialog.Show("Khim Tools", "Kh├┤ng t├¼m thß║Ñy t├ái liß╗çu Revit ─æang mß╗ƒ.");
                return Result.Cancelled;
            }

            try
            {
                Viewport preSelectedVp = null;

                // 1. Kiß╗âm tra xem ng╞░ß╗¥i d├╣ng ─æ├ú chß╗ìn sß║╡n Viewport tr├¬n Sheet ch╞░a
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 1)
                {
                    preSelectedVp = doc.GetElement(selectedIds.First()) as Viewport;
                }

                // 2. Mß╗ƒ form t╞░╞íng t├íc chuy├¬n nghiß╗çp (tß╗▒ ─æß╗Öng load viewport mß║½u nß║┐u c├│, hoß║╖c cho ph├⌐p pick tr├¬n form)
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

                // 3. Tiß║┐n h├ánh c─ân chß╗ënh tr├¬n tß╗½ng View/Schedule
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
                            // Bß║úng Schedule
                            ScheduleSheetInstance targetSched = doc.GetElement(targetItem.ViewportOrScheduleId) as ScheduleSheetInstance;
                            if (targetSched != null)
                            {
                                using (var tx = new Transaction(doc, $"Align Schedule {targetItem.ViewName}"))
                                {
                                    tx.Start();
                                    try
                                    {
                                        // T├¼m schedule mß║½u tr├¬n source sheet
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

                // L├ám mß╗¢i giao diß╗çn Active View
                uidoc.RefreshActiveView();

                string msgSummary = LanguageManager.IsEnglish
                    ? $"Alignment Completed!\n\n" +
                      $"ΓÇó Viewports successfully aligned: {viewportCount}\n" +
                      $"ΓÇó Schedules successfully aligned: {scheduleCount}\n" +
                      $"ΓÇó Arrange Mode: {mode}\n" +
                      (failedCount > 0 ? $"ΓÇó Errors: {failedCount}\n" : "")
                    : $"─É├ú ho├án tß║Ñt c─ân chß╗ënh vß╗ï tr├¡ Viewport & Ti├¬u ─æß╗ü bß║ún vß║╜!\n\n" +
                      $"ΓÇó Sß╗æ Viewport c─ân chß╗ënh th├ánh c├┤ng: {viewportCount}\n" +
                      $"ΓÇó Sß╗æ Bß║úng Schedule c─ân chß╗ënh th├ánh c├┤ng: {scheduleCount}\n" +
                      $"ΓÇó Chß║┐ ─æß╗Ö c─ân chß╗ënh: {mode}\n" +
                      (failedCount > 0 ? $"ΓÇó Lß╗ùi: {failedCount}\n" : "");

                TaskDialog.Show("Khim Tools ΓÇö Arrange Views & Title", msgSummary);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools ΓÇö Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
