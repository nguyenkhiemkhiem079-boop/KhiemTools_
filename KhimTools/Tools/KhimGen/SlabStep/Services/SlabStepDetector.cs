using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SlabStep.Models;

namespace KhimTools.SlabStep.Services
{
    public static class SlabStepDetector
    {
        public static SlabScanResult Scan(Document doc, View view, SlabScanOptions options)
        {
            options = options ?? new SlabScanOptions();
            var result = new SlabScanResult { StartedAt = DateTime.Now };
            result.Panels = SlabPanelScanner.Scan(doc, view, options.Scope).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < result.Panels.Count; i++)
            {
                SlabPanelInfo a = result.Panels[i];
                if (!a.IsSupported) continue;
                for (int j = i + 1; j < result.Panels.Count; j++)
                {
                    SlabPanelInfo b = result.Panels[j];
                    if (!b.IsSupported || !CanTouch(a.BoundingBox, b.BoundingBox, options.GeometryToleranceMm)) continue;
                    result.BoundingBoxPairs++;
                    string key = a.FloorId.IntegerValue < b.FloorId.IntegerValue ? a.FloorId + "|" + b.FloorId : b.FloorId + "|" + a.FloorId;
                    if (!seen.Add(key)) continue;
                    double delta = Math.Abs(a.TopElevation - b.TopElevation);
                    double deltaMm = SlabStepService.InternalToMillimetres(delta);
                    if (deltaMm < options.MinStepHeightMm || deltaMm > options.MaxStepHeightMm) continue;
                    result.GeometryComparisons++;
                    Floor high = a.TopElevation > b.TopElevation ? a.Floor : b.Floor;
                    Floor low = a.TopElevation > b.TopElevation ? b.Floor : a.Floor;
                    var boundaries = SlabStepService.FindSharedBoundaries(high, low, new SharedBoundaryOptions { GeometryToleranceMm = options.GeometryToleranceMm, MinimumLengthMm = options.MinSharedLengthMm });
                    if (boundaries.Count == 0) continue;
                    SlabPanelInfo highInfo = a.TopElevation > b.TopElevation ? a : b;
                    SlabPanelInfo lowInfo = a.TopElevation > b.TopElevation ? b : a;
                    result.Candidates.Add(new SlabStepCandidate { High = highInfo, Low = lowInfo, StepHeight = delta, Boundaries = boundaries });
                }
            }
            result.FinishedAt = DateTime.Now;
            return result;
        }

        private static bool CanTouch(BoundingBoxXYZ a, BoundingBoxXYZ b, double toleranceMm)
        {
            if (a == null || b == null) return true;
            double t = SlabStepService.MillimetresToInternal(toleranceMm);
            return a.Max.X + t >= b.Min.X && b.Max.X + t >= a.Min.X && a.Max.Y + t >= b.Min.Y && b.Max.Y + t >= a.Min.Y;
        }
    }
}
