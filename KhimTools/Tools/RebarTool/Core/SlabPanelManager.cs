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
            if (selectedPanelIds == null || selectedPanelIds.Count < 2) return false;

            var targetPanels = Panels.Where(p => selectedPanelIds.Contains(p.PanelId)).ToList();
            if (targetPanels.Count < 2) return false;

            // Gộp vào panel đầu tiên
            var primary = targetPanels[0];
            primary.IsMerged = true;
            primary.MergedChildrenIds = targetPanels.Select(p => p.PanelId).ToList();

            // Tính tổng kích thước
            double totalWidth = targetPanels.Max(p => p.WidthMm);
            double totalLength = targetPanels.Sum(p => p.LengthMm);
            primary.WidthMm = totalWidth;
            primary.LengthMm = totalLength;

            // Xóa các panel con khỏi danh sách chính, giữ lại panel đã gộp
            for (int i = 1; i < targetPanels.Count; i++)
            {
                Panels.Remove(targetPanels[i]);
            }

            return true;
        }

        public void DeletePanels(List<string> panelIdsToDelete)
        {
            if (panelIdsToDelete == null || !panelIdsToDelete.Any()) return;
            Panels.RemoveAll(p => panelIdsToDelete.Contains(p.PanelId));
        }

        public void AutoMergeAdjacent()
        {
            // Auto merge các panel cùng Level và có kích thước tương đồng
            var groups = Panels.GroupBy(p => p.LevelName).ToList();
            foreach (var group in groups)
            {
                var list = group.ToList();
                if (list.Count >= 2)
                {
                    MergeSelectedPanels(list.Take(2).Select(p => p.PanelId).ToList());
                }
            }
        }
    }
}
