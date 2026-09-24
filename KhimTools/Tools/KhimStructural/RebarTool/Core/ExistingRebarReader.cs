using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Đọc lại thép đã tồn tại (đã được tạo từ trước, kể cả bằng tay) trên 1 cột, dùng cho
    /// CmdColumnDrawing / CmdUpdateColumnDrawing khi không có sẵn dữ liệu từ Form.
    /// </summary>
    public static class ExistingRebarReader
    {
        public class RebarSummary
        {
            public int MainBarQty;
            public string MainBarLabel = "?";
            public int StirrupCount;
            public string StirrupLabel = "?";
            public double StirrupSpacingMm; // khoảng cách trung bình ước lượng giữa các đai

            public bool HasData => MainBarQty > 0 || StirrupCount > 0;
        }

        public static RebarSummary ReadFromColumn(Document doc, Element column)
        {
            var summary = new RebarSummary();

            var rebars = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(r => r.GetHostId() == column.Id)
                .ToList();

            Func<Rebar, RebarStyle> getRebarStyle = r =>
            {
                try
                {
                    ElementId shapeId = r.GetShapeId();
                    if (shapeId != null && shapeId != ElementId.InvalidElementId)
                    {
                        if (doc.GetElement(shapeId) is RebarShape shape)
                        {
                            return shape.RebarStyle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Could not determine the Rebar style for element " + r.Id + ".", ex);
                }
                throw new InvalidOperationException("Could not determine the Rebar style for element " + r.Id + ": its shape is missing or unresolved.");
            };

            var mainBars = rebars.Where(r => getRebarStyle(r) == RebarStyle.Standard).ToList();
            var stirrups = rebars.Where(r => getRebarStyle(r) == RebarStyle.StirrupTie).ToList();

            if (mainBars.Any())
            {
                summary.MainBarQty = mainBars.Sum(CountExistingPositions);
                summary.MainBarLabel = (doc.GetElement(mainBars.First().GetTypeId()) as RebarBarType)?.Name ?? "?";
            }

            if (stirrups.Any())
            {
                summary.StirrupCount = stirrups.Sum(CountExistingPositions);
                summary.StirrupLabel = (doc.GetElement(stirrups.First().GetTypeId()) as RebarBarType)?.Name ?? "?";

                var stations = new List<double>();
                foreach (Rebar stirrup in stirrups)
                    foreach (int positionIndex in GetExistingPositionIndices(stirrup))
                        stations.Add(GetStirrupStationElevation(stirrup, positionIndex));
                var uniqueStations = new List<double>();
                foreach (double z in stations.OrderBy(value => value))
                    if (uniqueStations.Count == 0 || Math.Abs(z - uniqueStations[uniqueStations.Count - 1]) > 1e-6)
                        uniqueStations.Add(z);
                if (uniqueStations.Count >= 2)
                {
                    double spanFeet = uniqueStations.Last() - uniqueStations.First();
                    double avgSpacingFeet = spanFeet / (uniqueStations.Count - 1);
                    summary.StirrupSpacingMm = UnitUtils.ConvertFromInternalUnits(avgSpacingFeet, UnitTypeId.Millimeters);
                }
            }

            return summary;
        }

        private static int CountExistingPositions(Rebar rebar) => GetExistingPositionIndices(rebar).Count;

        private static List<int> GetExistingPositionIndices(Rebar rebar)
        {
            int count = Math.Max(1, rebar.NumberOfBarPositions);
            var positions = new List<int>(count);
            for (int positionIndex = 0; positionIndex < count; positionIndex++)
                if (rebar.DoesBarExistAtPosition(positionIndex)) positions.Add(positionIndex);
            return positions;
        }

        private static double GetStirrupStationElevation(Rebar stirrup, int positionIndex)
        {
            IList<Curve> curves = stirrup.GetCenterlineCurves(false, true, false,
                MultiplanarOption.IncludeAllMultiplanarCurves, positionIndex);
            double[] elevations = curves.SelectMany(curve => curve.Tessellate())
                .Select(point => point.Z).OrderBy(z => z).ToArray();
            if (elevations.Length == 0)
                throw new InvalidOperationException("Could not resolve the centerline for stirrup element " + stirrup.Id +
                    " at bar position " + positionIndex + ".");
            if (elevations[elevations.Length - 1] - elevations[0] > 1e-4)
                throw new InvalidOperationException("Stirrup element " + stirrup.Id + " at bar position " + positionIndex +
                    " does not resolve to one horizontal column tie station.");
            return elevations[elevations.Length / 2];
        }
    }
}
