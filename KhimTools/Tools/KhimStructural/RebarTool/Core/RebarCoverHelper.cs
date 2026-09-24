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
            return GetFaceCover(floor, face);
        }

        /// <summary>
        /// Trả về cover (feet, đơn vị nội bộ Revit) tại 1 mặt cụ thể của cấu kiện.
        /// </summary>
        public static double GetColumnCover(Element column, RebarFace face = RebarFace.Exterior)
        {
            return GetFaceCover(column, face);
        }

        private static double GetFaceCover(Element host, RebarFace face)
        {
            if (host == null) return ToFeet(FallbackCoverMm);
            BuiltInParameter parameterId = CoverParameter(face);
            Parameter parameter = host.get_Parameter(parameterId);
            if (parameter != null && parameter.StorageType == StorageType.ElementId)
            {
                RebarCoverType faceCover = host.Document.GetElement(parameter.AsElementId()) as RebarCoverType;
                if (faceCover != null && faceCover.CoverDistance >= 0) return faceCover.CoverDistance;
            }

            // In-place families and stairs expose CLEAR_COVER rather than CLEAR_COVER_OTHER.
            if (face == RebarFace.Other)
            {
                Parameter genericCover = host.get_Parameter(BuiltInParameter.CLEAR_COVER);
                if (genericCover != null && genericCover.StorageType == StorageType.ElementId)
                {
                    RebarCoverType genericType = host.Document.GetElement(genericCover.AsElementId()) as RebarCoverType;
                    if (genericType != null && genericType.CoverDistance >= 0) return genericType.CoverDistance;
                }
            }

            RebarHostData hostData = RebarHostData.GetRebarHostData(host);
            try
            {
                RebarCoverType commonCover = hostData?.GetCommonCoverType();
                if (commonCover != null && commonCover.CoverDistance >= 0) return commonCover.CoverDistance;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                // Unsupported host categories may not expose RebarHostData cover access.
            }

            return ToFeet(FallbackCoverMm);
        }

        private static BuiltInParameter CoverParameter(RebarFace face)
        {
            switch (face)
            {
                case RebarFace.Top: return BuiltInParameter.CLEAR_COVER_TOP;
                case RebarFace.Bottom: return BuiltInParameter.CLEAR_COVER_BOTTOM;
                case RebarFace.Interior: return BuiltInParameter.CLEAR_COVER_INTERIOR;
                case RebarFace.Exterior: return BuiltInParameter.CLEAR_COVER_EXTERIOR;
                default: return BuiltInParameter.CLEAR_COVER_OTHER;
            }
        }

        /// <summary>
        /// Lấy hoặc khởi tạo mới 1 RebarCoverType trong Project Structural Settings của Revit.
        /// </summary>
        public static RebarCoverType GetOrCreateCoverType(Document doc, double coverMm)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (double.IsNaN(coverMm) || double.IsInfinity(coverMm) || coverMm <= 0)
                throw new ArgumentOutOfRangeException(nameof(coverMm), "Concrete cover must be a finite positive distance in millimeters.");

            double coverFeet = ToFeet(coverMm);
            double matchTolerance = ToFeet(0.1);
            string coverName = $"{coverMm:0} mm";

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarCoverType))
                .Cast<RebarCoverType>()
                .FirstOrDefault(ct => Math.Abs(ct.CoverDistance - coverFeet) <= matchTolerance);

            if (existing != null) return existing;

            try
            {
                RebarCoverType created = RebarCoverType.Create(doc, coverName, coverFeet);
                if (created != null && Math.Abs(created.CoverDistance - coverFeet) <= matchTolerance)
                    return created;
            }
            catch (Exception creationException)
            {
                RebarCoverType createdByRevit = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarCoverType))
                    .Cast<RebarCoverType>()
                    .FirstOrDefault(ct => Math.Abs(ct.CoverDistance - coverFeet) <= matchTolerance);
                if (createdByRevit != null) return createdByRevit;
                throw new InvalidOperationException("Revit could not create or resolve a concrete cover type at the requested distance of " + coverMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm.", creationException);
            }

            throw new InvalidOperationException("Revit did not create a concrete cover type at the requested distance of " + coverMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm.");
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
                    hostData.SetCommonCoverType(coverType);
                    count++;
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
