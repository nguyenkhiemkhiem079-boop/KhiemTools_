using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using KhimTools.Core.Revit;
using KhimTools.Architectural;
using KhimTools.Core;
using System.Diagnostics;

namespace KhimTools.Architectural.QuickArchi.Services
{
    /// <summary>
    /// Service thực thi tạo nhanh các phần tử kiến trúc (Tường, Phòng) trong Revit.
    /// </summary>
    public static class QuickArchiService
    {
        public sealed class WallCreationResult
        {
            public List<Wall> CreatedWalls { get; } = new List<Wall>();
            public int Failed { get; internal set; }
        }

        public static List<WallType> GetWallTypes(Document doc)
        {
            if (doc == null) return new List<WallType>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind != WallKind.Curtain) // Bỏ qua vách kính Curtain Wall
                .OrderBy(wt => wt.Name)
                .ToList();
        }

        public static List<Level> GetLevels(Document doc)
        {
            if (doc == null) return new List<Level>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        public static List<Curve> GetSelectedOrModelCurves(UIDocument uidoc)
        {
            var curves = new List<Curve>();
            if (uidoc == null) return curves;

            var doc = uidoc.Document;
            var selIds = uidoc.Selection.GetElementIds();

            foreach (var id in selIds.OrderBy(x => x.ToLongValue()))
            {
                var elem = doc.GetElement(id);
                if (elem is ModelCurve mc)
                {
                    curves.Add(mc.GeometryCurve);
                }
                else if (elem is DetailCurve dc)
                {
                    curves.Add(dc.GeometryCurve);
                }
            }

            return curves;
        }

        public static WallCreationResult CreateWallsFromCurves(
            Document doc,
            List<Curve> curves,
            WallType wallType,
            Level level,
            double heightMm,
            double offsetMm,
            bool isStructural)
        {
            var result = new WallCreationResult();
            if (doc == null || curves == null || wallType == null || level == null)
                throw new ArgumentException("A document, curves, wall type, and level are required.");
            if (wallType.Document != doc || level.Document != doc || wallType.Kind == WallKind.Curtain)
                throw new ArgumentException("The selected wall type and level must belong to this document; curtain wall types are not supported.");
            if (double.IsNaN(heightMm) || double.IsInfinity(heightMm) || heightMm <= 0 ||
                double.IsNaN(offsetMm) || double.IsInfinity(offsetMm))
                throw new ArgumentOutOfRangeException(nameof(heightMm), "Wall height must be finite and positive and offset must be finite.");
            if (curves.Count == 0) return result;

            var timer = Stopwatch.StartNew();
            double heightFt = RevitUnitService.MillimetresToFeet(heightMm);
            double offsetFt = RevitUnitService.MillimetresToFeet(offsetMm);

            using (var tx = new Transaction(doc, "K-TOOLS — Quick Archi Walls"))
            {
                TransactionBoundary.Start(tx, "Architectural.QuickArchi.Walls");
                try
                {
                    foreach (var curve in curves)
                    {
                        if (curve == null || !curve.IsBound || curve.Length <= 1e-6)
                        {
                            result.Failed++;
                            continue;
                        }
                        using (var sub = new SubTransaction(doc))
                        {
                            TransactionBoundary.Start(sub, "Architectural.QuickArchi.Walls");
                            try
                            {
                                Wall wall = Wall.Create(doc, curve, wallType.Id, level.Id, heightFt, offsetFt, false, isStructural);
                                if (wall == null || doc.GetElement(wall.Id) is not Wall)
                                    throw new InvalidOperationException("Wall creation did not produce a resolvable wall.");
                                TransactionBoundary.Commit(sub, "Architectural.QuickArchi.Walls");
                                result.CreatedWalls.Add(wall);
                            }
                            catch
                            {
                                TransactionBoundary.RollBack(sub, "Architectural.QuickArchi.Walls");
                                result.Failed++;
                            }
                        }
                    }
                    if (result.CreatedWalls.Count == 0) TransactionBoundary.RollBack(tx, "Architectural.QuickArchi.Walls");
                    else TransactionBoundary.Commit(tx, "Architectural.QuickArchi.Walls");
                }
                catch
                {
                    TransactionBoundary.RollBack(tx, "Architectural.QuickArchi.Walls");
                    result.CreatedWalls.Clear();
                    result.Failed = curves.Count;
                    throw;
                }
            }
            timer.Stop();
            ArchitecturalDiagnostics.Log("CmdQuickArchi", doc, "create-walls", curves.Count,
                result.CreatedWalls.Count, result.Failed, timer.Elapsed);
            return result;
        }

        public static int CreateRooms(Document doc, ViewPlan planView)
        {
            if (doc == null || planView == null || planView.GenLevel == null) return 0;

            var timer = Stopwatch.StartNew();
            int count = 0;
            using (var tx = new Transaction(doc, "K-TOOLS — Quick Archi Rooms"))
            {
                TransactionBoundary.Start(tx, "Architectural.QuickArchi.Rooms");
                try
                {
                    var topo = doc.Create.NewRooms2(planView.GenLevel);
                    var createdIds = topo == null ? new List<ElementId>() : topo.ToList();
                    count = createdIds.Count(id => doc.GetElement(id) is Room);
                    if (count == 0) TransactionBoundary.RollBack(tx, "Architectural.QuickArchi.Rooms");
                    else TransactionBoundary.Commit(tx, "Architectural.QuickArchi.Rooms");
                }
                catch
                {
                    TransactionBoundary.RollBack(tx, "Architectural.QuickArchi.Rooms");
                    throw;
                }
            }
            timer.Stop();
            ArchitecturalDiagnostics.Log("CmdQuickArchi", doc, "create-rooms", 1, count, 0, timer.Elapsed);
            return count;
        }
    }
}
