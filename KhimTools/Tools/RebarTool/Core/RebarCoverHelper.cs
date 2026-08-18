using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public enum RebarFace
    {
        Exterior,
        Interior,
        Top,
        Bottom,
        Other
    }

    /// <summary>
    /// Helper quản lý và cài đặt Lớp bê tông bảo vệ (Concrete Cover) toàn dự án.
    /// </summary>
    public static class RebarCoverHelper
    {
        private const double FallbackCoverMm = 25.0;

        public static double GetFloorCover(Element floor, RebarFace face)
        {
            if (floor == null) return ToFeet(FallbackCoverMm);
            RebarHostData hostData = RebarHostData.GetRebarHostData(floor);
            if (hostData == null) return ToFeet(FallbackCoverMm);

            try
            {
                RebarCoverType coverType = hostData.GetCommonCoverType();
                if (coverType != null) return coverType.CoverDistance;
            }
            catch { }

            return ToFeet(FallbackCoverMm);
        }

        /// <summary>
        /// Trả về cover (feet, đơn vị nội bộ Revit) tại 1 mặt cụ thể của cấu kiện.
        /// </summary>
        public static double GetColumnCover(Element column, RebarFace face = RebarFace.Exterior)
        {
            RebarHostData hostData = RebarHostData.GetRebarHostData(column);
            if (hostData == null)
                return ToFeet(FallbackCoverMm);

            RebarCoverType coverType = null;
            try
            {
                coverType = hostData.GetCommonCoverType();
            }
            catch
            {
                coverType = null;
            }

            if (coverType == null)
                return ToFeet(FallbackCoverMm);

            return coverType.CoverDistance;
        }

        /// <summary>
        /// Lấy hoặc khởi tạo mới 1 RebarCoverType trong Project Structural Settings của Revit.
        /// </summary>
        public static RebarCoverType GetOrCreateCoverType(Document doc, double coverMm)
        {
            double coverFeet = ToFeet(coverMm);
            string coverName = $"{coverMm:0} mm";

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarCoverType))
                .Cast<RebarCoverType>()
                .FirstOrDefault(ct => Math.Abs(ct.CoverDistance - coverFeet) < 0.001);

            if (existing != null) return existing;

            try
            {
                return RebarCoverType.Create(doc, coverName, coverFeet);
            }
            catch
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarCoverType))
                    .Cast<RebarCoverType>()
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Gán RebarCoverType cho toàn bộ các phần tử thuộc 1 Category trong mô hình.
        /// </summary>
        public static int ApplyCoverToCategory(Document doc, BuiltInCategory category, RebarCoverType coverType)
        {
            if (coverType == null) return 0;

            var elements = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToList();

            int count = 0;
            foreach (var elem in elements)
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(elem);
                if (hostData != null)
                {
                    try
                    {
                        hostData.SetCommonCoverType(coverType);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RebarCoverHelper] SetCommonCoverType failed on element {elem.Id}: {ex.Message}");
                    }
                }
            }
            return count;
        }

        public static double ToFeet(double mm) =>
            UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        public static double ToMm(double feet) =>
            UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
    }
}
