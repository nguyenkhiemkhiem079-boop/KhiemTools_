using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public class JointConfinementResult
    {
        public bool IsValid { get; set; } = true;
        public Element Column { get; set; }
        public Element Beam { get; set; }
        public BoundingBoxXYZ JointZone { get; set; }
        public double JointHeightMm { get; set; }
        public int RecommendedTieCount { get; set; }
        public double RecommendedTieSpacingMm { get; set; }
        public bool BeamBarsPassThrough { get; set; }
        public bool BeamBarsRequireHooks { get; set; }
        public double AvailableAnchorageLengthMm { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
    }

    /// <summary>
    /// Bộ xử lý nút khung Dầm - Cột (Section 19 Beam-Column Joint Engine):
    /// Thẩm định tương quan không gian 3D, đai giằng nút khung (Joint Confinement Ties),
    /// điều kiện neo thép dầm (Pass-through vs 90° Hook) và kiểm tra chống nghẽn thép (Congestion).
    /// </summary>
    public static class BeamColumnJointEngine
    {
        public const double MinJointTieSpacingMm = 100.0;
        public const double MaxJointTieSpacingMm = 150.0;

        /// <summary>
        /// Phân tích không gian nút khung giữa Dầm và Cột
        /// </summary>
        public static JointConfinementResult AnalyzeJoint(Element column, Element beam, bool isExteriorJoint = false)
        {
            var res = new JointConfinementResult
            {
                Column = column,
                Beam = beam,
                BeamBarsRequireHooks = isExteriorJoint,
                BeamBarsPassThrough = !isExteriorJoint
            };

            if (column == null || beam == null)
            {
                res.IsValid = false;
                res.Diagnostics.Add("Column or Beam element is null in joint analysis.");
                return res;
            }

            BoundingBoxXYZ colBb = column.get_BoundingBox(null);
            BoundingBoxXYZ beamBb = beam.get_BoundingBox(null);

            if (colBb == null || beamBb == null)
            {
                res.IsValid = false;
                res.Diagnostics.Add("Cannot retrieve BoundingBox for joint elements.");
                return res;
            }

            // Vùng nút = giao thoa độ cao của dầm với tiết diện mặt bằng của cột
            double jointZMin = beamBb.Min.Z;
            double jointZMax = beamBb.Max.Z;
            res.JointHeightMm = UnitUtils.ConvertFromInternalUnits(Math.Max(0, jointZMax - jointZMin), UnitTypeId.Millimeters);

            // Bề rộng cột theo phương dầm để xác định chiều dài neo khả dụng
            double colDepthFeet = Math.Abs(colBb.Max.X - colBb.Min.X); // Giả định sơ bộ
            res.AvailableAnchorageLengthMm = UnitUtils.ConvertFromInternalUnits(colDepthFeet, UnitTypeId.Millimeters) - 60.0; // Trừ cover 2 bên

            // Tính số lượng đai giằng nút khung (Eurocode 2 / TCVN 5574 yêu cầu duy trì đai cột trong nút khung)
            if (res.JointHeightMm > 100.0)
            {
                res.RecommendedTieSpacingMm = Math.Min(150.0, Math.Max(100.0, res.JointHeightMm / 4.0));
                res.RecommendedTieCount = (int)Math.Floor(res.JointHeightMm / res.RecommendedTieSpacingMm);
                res.Diagnostics.Add($"Joint Zone: Height = {res.JointHeightMm:F0}mm, Recommended Ties = {res.RecommendedTieCount} @ {res.RecommendedTieSpacingMm:F0}mm.");
            }

            if (isExteriorJoint)
            {
                res.Diagnostics.Add("Exterior Joint: Beam longitudinal bars MUST anchor with standard 90° hooks turned inward.");
            }
            else
            {
                res.Diagnostics.Add("Interior Joint: Continuous beam bars pass through the joint without hooks.");
            }

            return res;
        }

        /// <summary>
        /// Kiểm tra mật độ / độ thông thoáng cốt thép trong nút khung.
        /// PHÂN LOẠI: Quy chuẩn biện pháp thi công & cấu tạo dự án (Constructability / Project Detailing Rule).
        /// (LƯU Ý: EN 1992-1-1 không có điều khoản quy định trực tiếp tỷ lệ 8% cho nút khung;
        /// giới hạn 8% này được kế thừa từ ngưỡng hàm lượng thép tối đa tại vị trí nối chồng cột EN 1992-1-1 Cl. 9.5.2(3)
        /// và hướng dẫn thi công thực tế nhằm chống rỗ tổ ong, đảm bảo bê tông chèn lọt qua khe cốt thép).
        /// </summary>
        public static bool ValidateJointCongestion(int columnBarCount, int beamBarCount, double jointAreaMm2, out string warning)
        {
            warning = "";
            double totalBars = columnBarCount + beamBarCount;
            double approxBarArea = totalBars * (Math.PI * 25.0 * 25.0 / 4.0); // Giả định thanh D25
            double ratio = (jointAreaMm2 > 0) ? (approxBarArea / jointAreaMm2) * 100.0 : 0;

            if (ratio > 8.0) // Vượt quá 8% diện tích mặt cắt ngang nút khung (Constructability threshold)
            {
                warning = $"Constructability warning: High rebar congestion in beam-column joint ({ratio:F1}% steel ratio > 8.0% constructability limit). Risk of honeycombing; aggregate compaction may be hindered.";
                return false;
            }

            return true;
        }
    }
}
