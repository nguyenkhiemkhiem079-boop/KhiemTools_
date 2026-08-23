using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.GridLevel.Forms;
using KhimTools.GridLevel.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.GridLevel.Commands
{
    /// <summary>
    /// Command: Tạo Hệ Lưới Trục & Cao Độ Tầng, Mặt Bằng Tự Động trong Revit.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAutoGridPlan : IExternalCommand
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
                var form = new AutoGridPlanForm(uidoc);
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                int gridsCreated = 0;
                int levelsCreated = 0;
                int viewsCreated = 0;

                using (var tg = new TransactionGroup(doc, "Auto Generate Grids, Levels and Plans"))
                {
                    tg.Start();

                    // 1. Tạo Hệ Lưới Trục (Grids)
                    if (form.ShouldCreateGrids && form.GridsResult != null)
                    {
                        using (var tx = new Transaction(doc, "Create Grids"))
                        {
                            tx.Start();
                            try
                            {
                                var grids = GridGeneratorService.CreateGrids(doc, form.GridsResult, uidoc.ActiveView);
                                gridsCreated = grids.Count;
                                tx.Commit();
                            }
                            catch (Exception ex)
                            {
                                tx.RollBack();
                                TaskDialog.Show("Lỗi tạo Grid", ex.Message);
                            }
                        }
                    }

                    // 2. Tạo Cao Độ Tầng & Mặt Bằng (Levels & Plans)
                    if (form.ShouldCreateLevels && form.LevelsResult != null && form.LevelsResult.Any())
                    {
                        using (var tx = new Transaction(doc, "Create Levels and Plans"))
                        {
                            tx.Start();
                            try
                            {
                                var res = LevelPlanGeneratorService.CreateLevelsAndPlans(doc, form.LevelsResult);
                                levelsCreated = res.levelsCreated;
                                viewsCreated = res.viewsCreated;
                                tx.Commit();
                            }
                            catch (Exception ex)
                            {
                                tx.RollBack();
                                TaskDialog.Show("Lỗi tạo Level/Plan", ex.Message);
                            }
                        }
                    }

                    tg.Assimilate();
                }

                uidoc.RefreshActiveView();

                string msg = LanguageManager.IsEnglish
                    ? $"Project Setup Completed!\n\n" +
                      $"• Grids created: {gridsCreated}\n" +
                      $"• Levels created/updated: {levelsCreated}\n" +
                      $"• Plan Views generated: {viewsCreated}"
                    : $"Đã hoàn tất khởi tạo Lưới Trục & Mặt Bằng!\n\n" +
                      $"• Số Lưới trục (Grids) đã tạo: {gridsCreated}\n" +
                      $"• Số Cao độ tầng (Levels) đã tạo: {levelsCreated}\n" +
                      $"• Số Khung nhìn mặt bằng (Plans) đã tạo: {viewsCreated}";

                TaskDialog.Show("Khim Tools — Auto Grid & Plan", msg);

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
