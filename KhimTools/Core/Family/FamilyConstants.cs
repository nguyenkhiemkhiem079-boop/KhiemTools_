using System;

namespace KhimTools.Core.Family
{
    /// <summary>
    /// Định nghĩa các hằng số tên Family tiêu chuẩn và thư mục con trong bộ thư viện K-TOOLS.
    /// </summary>
    public static class FamilyConstants
    {
        /// <summary>Family nách sàn giật cấp tiêu chuẩn.</summary>
        public const string RincoAnStep = "RINCO_AN_Step";

        /// <summary>Phần mở rộng file family Revit.</summary>
        public const string RfaExtension = ".rfa";

        /// <summary>Thư mục chứa family tiêu chuẩn trong bundle.</summary>
        public const string StandardSubfolder = "Standard";

        /// <summary>Thư mục gốc chứa families trong bundle.</summary>
        public const string FamiliesFolder = "Families";

        /// <summary>Thư mục chứa rebar shapes trong bundle.</summary>
        public const string RebarShapesFolder = "RebarShapes";
    }
}
