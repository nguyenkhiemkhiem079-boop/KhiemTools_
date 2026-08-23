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
                catch { }
                return RebarStyle.Standard;
            };

            var mainBars = rebars.Where(r => getRebarStyle(r) == RebarStyle.Standard).ToList();
            var stirrups = rebars.Where(r => getRebarStyle(r) == RebarStyle.StirrupTie).ToList();

            if (mainBars.Any())
            {
                summary.MainBarQty = mainBars.Count;
                summary.MainBarLabel = (doc.GetElement(mainBars.First().GetTypeId()) as RebarBarType)?.Name ?? "?";
            }

            if (stirrups.Any())
            {
                summary.StirrupCount = stirrups.Count;
                summary.StirrupLabel = (doc.GetElement(stirrups.First().GetTypeId()) as RebarBarType)?.Name ?? "?";

                var zs = stirrups.Select(GetApproxZ).OrderBy(z => z).ToList();
                if (zs.Count >= 2)
                {
                    double spanFeet = zs.Last() - zs.First();
                    double avgSpacingFeet = spanFeet / (zs.Count - 1);
                    summary.StirrupSpacingMm = UnitUtils.ConvertFromInternalUnits(avgSpacingFeet, UnitTypeId.Millimeters);
                }
            }

            return summary;
        }

        private static double GetApproxZ(Rebar r)
        {
            BoundingBoxXYZ bb = r.get_BoundingBox(null);
            return bb != null ? (bb.Min.Z + bb.Max.Z) / 2.0 : 0.0;
        }
    }
}
