using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    public class SlabPanelManager
    {
        public List<SlabPanel> Panels { get; private set; } = new List<SlabPanel>();

        public void InitializeFromFloors(Document doc, List<Floor> floors)
        {
            Panels.Clear();
            if (doc == null || floors == null || !floors.Any()) return;

            int index = 1;
            foreach (var floor in floors)
            {
                var profile = SlabGeometryHelper.AnalyzeSlab(doc, floor);
                if (profile == null) continue;

                var panel = new SlabPanel
                {
                    PanelId = $"P{index++}",
                    HostFloorId = floor.Id,
                    HostFloor = floor,
                    FloorName = floor.Name,
                    LevelName = profile.LevelName,
                    Boundary = profile.OuterBoundary,
                    Openings = profile.InnerOpenings ?? new List<CurveLoop>(),
                    WidthMm = profile.WidthMm,
                    LengthMm = profile.LengthMm,
                    ThicknessMm = profile.ThicknessMm,
                    ThicknessFeet = profile.ThicknessFeet,
                    CoverTopFeet = profile.CoverTopFeet,
                    CoverBottomFeet = profile.CoverBottomFeet,
                    IsSelected = true
                };

                // Trích xuất các cạnh
                if (profile.OuterBoundary != null)
                {
                    int edgeIdx = 0;
                    foreach (Curve curve in profile.OuterBoundary)
                    {
                        var edge = new SlabPanelEdge
                        {
                            EdgeIndex = edgeIdx++,
                            EdgeCurve = curve,
                            EdgeType = SlabPanelEdgeType.BeamSupport,
                            SkipTopHat = false,
                            SkipBottomMesh = false
                        };
                        panel.Edges.Add(edge);
                    }
                }

                Panels.Add(panel);
            }
        }

        public bool MergeSelectedPanels(List<string> selectedPanelIds)
        {
            // A merged panel needs a real polygon union and a host mapping for every
            // source floor. Until that exists, mutating this list can silently skip
            // reinforcement on all but the first floor.
            return false;
        }

        public void DeletePanels(List<string> panelIdsToDelete)
        {
            if (panelIdsToDelete == null || !panelIdsToDelete.Any()) return;
            Panels.RemoveAll(p => panelIdsToDelete.Contains(p.PanelId));
        }

        public void AutoMergeAdjacent()
        {
            // Disabled until adjacency and polygon-union support are implemented.
        }
    }
}
