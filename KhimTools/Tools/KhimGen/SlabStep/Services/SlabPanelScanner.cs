using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SlabStep.Models;

namespace KhimTools.SlabStep.Services
{
    public static class SlabPanelScanner
    {
        public static IReadOnlyList<SlabPanelInfo> Scan(Document doc, View view, SlabScanScope scope)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            FilteredElementCollector collector = scope == SlabScanScope.ActiveView && view != null
                ? new FilteredElementCollector(doc, view.Id)
                : new FilteredElementCollector(doc);
            IEnumerable<Floor> floors = collector.OfClass(typeof(Floor)).WhereElementIsNotElementType().Cast<Floor>();
            if (scope == SlabScanScope.CurrentLevel && view?.GenLevel != null)
                floors = floors.Where(x => x.LevelId == view.GenLevel.Id);
            var result = new List<SlabPanelInfo>();
            foreach (Floor floor in floors)
            {
                try { result.Add(ReadPanel(floor)); }
                catch (Exception ex)
                {
                    result.Add(new SlabPanelInfo { Floor = floor, LevelId = floor.LevelId, FloorTypeId = floor.GetTypeId(), IsSupported = false, UnsupportedReason = ex.Message });
                }
            }
            return result;
        }

        public static SlabPanelInfo ReadPanel(Floor floor)
        {
            if (floor == null) throw new ArgumentNullException(nameof(floor));
            var info = new SlabPanelInfo { Floor = floor, LevelId = floor.LevelId, FloorTypeId = floor.GetTypeId(), BoundingBox = floor.get_BoundingBox(null) };
            ElementType type = floor.Document.GetElement(floor.GetTypeId()) as ElementType;
            info.TypeName = type?.Name ?? "";
            info.LevelName = (floor.Document.GetElement(floor.LevelId) as Level)?.Name ?? "";
            info.Thickness = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble() ?? 0;
            info.TopElevation = SlabStepService.GetFloorTopElevation(floor);
            info.BottomElevation = info.TopElevation - info.Thickness;
            Level level = floor.Document.GetElement(floor.LevelId) as Level;
            info.HeightOffset = info.TopElevation - (level?.Elevation ?? 0);
            info.BoundaryCurves = SlabStepService.GetFloorBoundaryCurves(floor.Document, floor);
            info.BoundaryLoops = ExtractLoops(info.BoundaryCurves);
            info.IsHorizontal = true;
                info.IsShapeEdited = info.BoundaryCurves.Any(x => !(x is Line));
            info.IsSupported = !info.IsShapeEdited && info.BoundaryCurves.Count > 0;
            info.UnsupportedReason = info.IsSupported ? "" : "Sloped, shape-edited or unsupported boundary geometry";
            return info;
        }

        private static IList<CurveLoop> ExtractLoops(IList<Curve> curves)
        {
            var loops = new List<CurveLoop>();
            if (curves == null || curves.Count == 0) return loops;
            var loop = new CurveLoop();
            foreach (Curve curve in curves) { if (curve != null) loop.Append(curve); }
            if (loop.Any()) loops.Add(loop);
            return loops;
        }
    }
}
