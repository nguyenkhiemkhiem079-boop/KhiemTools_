using System;
using Autodesk.Revit.DB;

namespace KhimTools.Core.Family
{
    /// <summary>
    /// Triển khai IFamilyLoadOptions chuẩn của Revit để quản lý hành vi nạp đè Family:
    /// - Quyết định ghi đè family đã tồn tại trong dự án.
    /// - Cho phép bảo toàn hoặc ghi đè giá trị tham số (parameter values).
    /// </summary>
    public class KhimFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OverwriteExisting { get; set; }
        public bool OverwriteParameterValues { get; set; }
        public FamilySource SharedFamilySource { get; set; }

        public KhimFamilyLoadOptions(bool overwriteExisting = true, bool overwriteParameterValues = true)
        {
            OverwriteExisting = overwriteExisting;
            OverwriteParameterValues = overwriteParameterValues;
            SharedFamilySource = FamilySource.Family;
        }

        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = OverwriteParameterValues;
            return OverwriteExisting;
        }

        public bool OnSharedFamilyFound(Autodesk.Revit.DB.Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = SharedFamilySource;
            overwriteParameterValues = OverwriteParameterValues;
            return OverwriteExisting;
        }
    }
}
