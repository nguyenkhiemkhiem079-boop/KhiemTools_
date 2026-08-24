using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.OverrideTool.Commands
{
    internal static class QuickColorHelper
    {
        public static Result ApplyColor(ExternalCommandData commandData, Color color, string transactionName)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var doc = uidoc.Document;
            var view = doc.ActiveView;
            var selIds = uidoc.Selection.GetElementIds().ToList();

            if (!selIds.Any())
            {
                TaskDialog.Show("K-TOOLS Graphic Overdrive",
                    LanguageManager.IsEnglish
                        ? "Please select at least 1 element in Revit to apply color override."
                        : "Vui lòng chọn ít nhất 1 đối tượng trong View để áp dụng màu override.");
                return Result.Cancelled;
            }

            try
            {
                var solidPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                using (var t = new Transaction(doc, transactionName))
                {
                    t.Start();

                    foreach (var id in selIds)
                    {
                        var ogs = view.GetElementOverrides(id);

                        if (solidPattern != null)
                        {
                            ogs = ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                            ogs = ogs.SetCutForegroundPatternId(solidPattern.Id);
                        }

                        ogs = ogs.SetSurfaceForegroundPatternColor(color);
                        ogs = ogs.SetSurfaceForegroundPatternVisible(true);
                        ogs = ogs.SetCutForegroundPatternColor(color);
                        ogs = ogs.SetCutForegroundPatternVisible(true);
                        ogs = ogs.SetProjectionLineColor(color);
                        ogs = ogs.SetCutLineColor(color);

                        view.SetElementOverrides(id, ogs);
                    }

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("K-TOOLS Error", ex.Message);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideRed : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(237, 28, 36), "K-TOOLS: Override Color Red");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideOrange : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(255, 127, 39), "K-TOOLS: Override Color Orange");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideYellow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(255, 242, 0), "K-TOOLS: Override Color Yellow");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideGreen : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(34, 177, 76), "K-TOOLS: Override Color Green");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideCyan : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(0, 162, 232), "K-TOOLS: Override Color Cyan");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideBlue : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(0, 0, 255), "K-TOOLS: Override Color Blue");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideMagenta : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(255, 0, 255), "K-TOOLS: Override Color Magenta");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideGray : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickColorHelper.ApplyColor(commandData, new Color(127, 127, 127), "K-TOOLS: Override Color Gray");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdOverrideCustom : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                dlg.AllowFullOpen = true;
                dlg.AnyColor = true;
                dlg.Color = System.Drawing.Color.Red;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var revitColor = new Color(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                    return QuickColorHelper.ApplyColor(commandData, revitColor, "K-TOOLS: Override Custom Color");
                }
            }
            return Result.Cancelled;
        }
    }
}
