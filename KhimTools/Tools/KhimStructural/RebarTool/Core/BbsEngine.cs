using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class BbsItem
    {
        public string BarMark { get; set; } = "";
        public string HostName { get; set; } = "";
        public string HostCategory { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string ShapeCode { get; set; } = "Shape 00";
        public double DiameterMm { get; set; }
        public int Quantity { get; set; } = 1;
        public double CutLengthMm { get; set; }
        public double TotalLengthM => (CutLengthMm / 1000.0) * Quantity;
        public double UnitWeightKgPerM => 0.006165 * DiameterMm * DiameterMm; // Công thức d^2 / 162
        public double TotalWeightKg => TotalLengthM * UnitWeightKgPerM;

        public double DimA_Mm { get; set; }
        public double DimB_Mm { get; set; }
        public double DimC_Mm { get; set; }
        public double DimD_Mm { get; set; }
        public double DimE_Mm { get; set; }

        public string Comments { get; set; } = "";
    }

    /// <summary>
    /// Engine thống kê cốt thép chuyên nghiệp (Bar Bending Schedule - BBS)
    /// chuẩn kỹ thuật xây dựng và tiêu chuẩn ISO 3766 / BS 8666 / TCVN 5574.
    /// </summary>
    public static class BbsEngine
    {
        /// <summary>
        /// Trích xuất bảng thống kê thép (BBS) từ danh sách Rebars hoặc từ cấu kiện Host
        /// </summary>
        public static List<BbsItem> ExtractBbs(Document doc, IEnumerable<Rebar> rebars, string markPrefix = "RB")
        {
            var rawItems = new List<BbsItem>();
            if (rebars == null) return rawItems;

            int index = 1;
            foreach (var rebar in rebars)
            {
                if (rebar == null || !rebar.IsValidObject) continue;

                var item = new BbsItem();
                item.BarMark = $"{markPrefix}-{index:D2}";
                index++;

                // Host info
                ElementId hostId = rebar.GetHostId();
                if (hostId != ElementId.InvalidElementId)
                {
                    Element host = doc.GetElement(hostId);
                    if (host != null)
                    {
                        item.HostName = host.Name;
                        item.HostCategory = host.Category?.Name ?? "";
                        Parameter lvlParam = host.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                                          ?? host.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                                          ?? host.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                        if (lvlParam != null && lvlParam.HasValue)
                        {
                            Element lvl = doc.GetElement(lvlParam.AsElementId());
                            item.LevelName = lvl?.Name ?? "";
                        }
                    }
                }

                // Đường kính thép
                Parameter diaParam = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER);
                if (diaParam != null && diaParam.HasValue)
                {
                    item.DiameterMm = Math.Round(UnitUtils.ConvertFromInternalUnits(diaParam.AsDouble(), UnitTypeId.Millimeters), 1);
                }

                // Số lượng thanh (Rebar Quantity)
                item.Quantity = Math.Max(1, rebar.Quantity);

                // Chiều dài cắt uốn (Total bar length)
                Parameter lenParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                if (lenParam != null && lenParam.HasValue)
                {
                    item.CutLengthMm = Math.Round(UnitUtils.ConvertFromInternalUnits(lenParam.AsDouble(), UnitTypeId.Millimeters), 0);
                }

                // Rebar Shape Name
                RebarShape shape = doc.GetElement(rebar.GetShapeId()) as RebarShape;
                item.ShapeCode = shape?.Name ?? "Shape 00";

                // Phân đoạn kích thước uốn (A, B, C, D, E)
                item.DimA_Mm = GetParamMm(rebar, "A", "VNDC_L1");
                item.DimB_Mm = GetParamMm(rebar, "B", "VNDC_L2");
                item.DimC_Mm = GetParamMm(rebar, "C", "VNDC_L3");
                item.DimD_Mm = GetParamMm(rebar, "D", "VNDC_L4");
                item.DimE_Mm = GetParamMm(rebar, "E", "VNDC_L5");

                // Comments
                Parameter commParam = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                item.Comments = commParam?.AsString() ?? "";

                rawItems.Add(item);
            }

            return rawItems;
        }

        /// <summary>
        /// Tổng hợp (Gom nhóm) các thanh thép có cùng Tiết diện, Hình dạng và Chiều dài cắt uốn
        /// </summary>
        public static List<BbsItem> AggregateBbs(List<BbsItem> items)
        {
            if (items == null || !items.Any()) return new List<BbsItem>();

            var grouped = items
                .GroupBy(i => new { i.DiameterMm, i.ShapeCode, CutLen = (int)Math.Round(i.CutLengthMm) })
                .Select((g, idx) =>
                {
                    var first = g.First();
                    return new BbsItem
                    {
                        BarMark = $"M-{idx + 1:D2}",
                        HostName = string.Join(", ", g.Select(x => x.HostName).Distinct().Take(3)),
                        HostCategory = first.HostCategory,
                        LevelName = first.LevelName,
                        ShapeCode = first.ShapeCode,
                        DiameterMm = first.DiameterMm,
                        Quantity = g.Sum(x => x.Quantity),
                        CutLengthMm = g.Average(x => x.CutLengthMm),
                        DimA_Mm = first.DimA_Mm,
                        DimB_Mm = first.DimB_Mm,
                        DimC_Mm = first.DimC_Mm,
                        DimD_Mm = first.DimD_Mm,
                        DimE_Mm = first.DimE_Mm,
                        Comments = first.Comments
                    };
                })
                .OrderBy(x => x.DiameterMm)
                .ThenBy(x => x.CutLengthMm)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Xuất bảng thống kê thép ra định dạng CSV tương thích 100% Microsoft Excel & Revit Schedules
        /// </summary>
        public static string ExportToCsv(IEnumerable<BbsItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BarMark,Host,Level,ShapeCode,Diameter_mm,Quantity,CutLength_mm,TotalLength_m,UnitWeight_kg_m,TotalWeight_kg,A_mm,B_mm,C_mm,D_mm,E_mm,Comments");

            if (items != null)
            {
                foreach (var it in items)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4},{5},{6:F0},{7:F2},{8:F3},{9:F2},{10:F0},{11:F0},{12:F0},{13:F0},{14:F0},\"{15}\"",
                        it.BarMark,
                        it.HostName,
                        it.LevelName,
                        it.ShapeCode,
                        it.DiameterMm,
                        it.Quantity,
                        it.CutLengthMm,
                        it.TotalLengthM,
                        it.UnitWeightKgPerM,
                        it.TotalWeightKg,
                        it.DimA_Mm,
                        it.DimB_Mm,
                        it.DimC_Mm,
                        it.DimD_Mm,
                        it.DimE_Mm,
                        it.Comments));
                }
            }

            return sb.ToString();
        }

        private static double GetParamMm(Rebar rebar, params string[] paramNames)
        {
            foreach (var name in paramNames)
            {
                Parameter p = rebar.LookupParameter(name);
                if (p != null && p.HasValue)
                {
                    return Math.Round(UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters), 0);
                }
            }
            return 0;
        }
    }
}
