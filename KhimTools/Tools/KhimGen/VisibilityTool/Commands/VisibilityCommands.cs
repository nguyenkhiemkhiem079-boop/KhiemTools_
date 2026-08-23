using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.VisibilityTool.Services;

namespace KhimTools.VisibilityTool.Commands
{
    // ════════════════════════════════════════════════════════════════════════════════
    // 1. CÁC LỆNH HIỆN THỊ (SHOW COMMANDS)
    // ════════════════════════════════════════════════════════════════════════════════

    // Kiến trúc
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowWindow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Window", true, BuiltInCategory.OST_Windows);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowDoor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Door", true, BuiltInCategory.OST_Doors);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowCeiling : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Ceiling", true, BuiltInCategory.OST_Ceilings);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowRoof : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Roof", true, BuiltInCategory.OST_Roofs);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowStair : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Stair", true, BuiltInCategory.OST_Stairs);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowRailing : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Railing", true, BuiltInCategory.OST_StairsRailing, BuiltInCategory.OST_RailingSystem);
            return Result.Succeeded;
        }
    }

    // Kết cấu
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowColumn : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Column", true, BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Columns);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowFraming : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Framing", true, BuiltInCategory.OST_StructuralFraming);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowFloor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Floor", true, BuiltInCategory.OST_Floors);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowWall : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Wall", true, BuiltInCategory.OST_Walls);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowFoundation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Foundation", true, BuiltInCategory.OST_StructuralFoundation);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Rebar", true, BuiltInCategory.OST_Rebar, BuiltInCategory.OST_FabricReinforcement);
            return Result.Succeeded;
        }
    }

    // Định vị & Chú thích
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowGrid : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Grid", true, BuiltInCategory.OST_Grids);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowLevel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Level", true, BuiltInCategory.OST_Levels);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowSection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Section", true, BuiltInCategory.OST_Sections, BuiltInCategory.OST_Viewers);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowElevation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Elevation", true, BuiltInCategory.OST_Elev);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdShowTag : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetTagVisibility(uidoc.Document, uidoc.ActiveView, true);
            return Result.Succeeded;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════
    // 2. CÁC LỆNH ẨN (HIDE COMMANDS)
    // ════════════════════════════════════════════════════════════════════════════════

    // Kiến trúc
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideWindow : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Window", false, BuiltInCategory.OST_Windows);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideDoor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Door", false, BuiltInCategory.OST_Doors);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideCeiling : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Ceiling", false, BuiltInCategory.OST_Ceilings);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideRoof : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Roof", false, BuiltInCategory.OST_Roofs);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideStair : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Stair", false, BuiltInCategory.OST_Stairs);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideRailing : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Railing", false, BuiltInCategory.OST_StairsRailing, BuiltInCategory.OST_RailingSystem);
            return Result.Succeeded;
        }
    }

    // Kết cấu
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideColumn : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Column", false, BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Columns);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideFraming : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Framing", false, BuiltInCategory.OST_StructuralFraming);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideFloor : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Floor", false, BuiltInCategory.OST_Floors);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideWall : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Wall", false, BuiltInCategory.OST_Walls);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideFoundation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Foundation", false, BuiltInCategory.OST_StructuralFoundation);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideRebar : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Rebar", false, BuiltInCategory.OST_Rebar, BuiltInCategory.OST_FabricReinforcement);
            return Result.Succeeded;
        }
    }

    // Định vị & Chú thích
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideGrid : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Grid", false, BuiltInCategory.OST_Grids);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideLevel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Level", false, BuiltInCategory.OST_Levels);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideSection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Section", false, BuiltInCategory.OST_Sections, BuiltInCategory.OST_Viewers);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideElevation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetCategoryVisibility(uidoc.Document, uidoc.ActiveView, "Elevation", false, BuiltInCategory.OST_Elev);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdHideTag : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            CategoryVisibilityService.SetTagVisibility(uidoc.Document, uidoc.ActiveView, false);
            return Result.Succeeded;
        }
    }
}