using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.GridLevel.Forms;
using KhimTools.GridLevel.Services;

namespace KhimTools.GridLevel.Commands
{
    // 1. Tạo Grid
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateGrid : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            using (var form = new AutoGridPlanForm(uidoc))
            {
                form.ShowDialog();
            }
            return Result.Succeeded;
        }
    }

    // 2. Tạo Level
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateLevel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            using (var form = new AutoGridPlanForm(uidoc))
            {
                form.ShowDialog();
            }
            return Result.Succeeded;
        }
    }

    // 3. Cắt Level
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCutLevel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.CutLevel(uidoc.Document, uidoc.ActiveView, selIds);
            return Result.Succeeded;
        }
    }

    // 4. Level Bubble
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdLevelBubble : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.ToggleDatumBubble(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Levels, selIds);
            return Result.Succeeded;
        }
    }

    // 5. Chuyển Level 2D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdConvertLevel2D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.SetDatumExtent(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Levels, true, selIds);
            return Result.Succeeded;
        }
    }

    // 6. Chuyển Level 3D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdConvertLevel3D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.SetDatumExtent(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Levels, false, selIds);
            return Result.Succeeded;
        }
    }

    // 7. Cắt Grid 3D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCutGrid3D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            DatumManagementService.CutGrid3D(uidoc.Document, uidoc.ActiveView);
            return Result.Succeeded;
        }
    }

    // 8. Trim Grid 2D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdTrimGrid2D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.TrimGrid2D(uidoc.Document, uidoc.ActiveView, selIds);
            return Result.Succeeded;
        }
    }

    // 9. Grid Bubble
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdGridBubble : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.ToggleDatumBubble(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Grids, selIds);
            return Result.Succeeded;
        }
    }

    // 10. Chuyển Grid 2D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdConvertGrid2D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.SetDatumExtent(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Grids, true, selIds);
            return Result.Succeeded;
        }
    }

    // 11. Chuyển Grid 3D
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdConvertGrid3D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            var selIds = uidoc.Selection.GetElementIds();
            DatumManagementService.SetDatumExtent(uidoc.Document, uidoc.ActiveView, BuiltInCategory.OST_Grids, false, selIds);
            return Result.Succeeded;
        }
    }
}