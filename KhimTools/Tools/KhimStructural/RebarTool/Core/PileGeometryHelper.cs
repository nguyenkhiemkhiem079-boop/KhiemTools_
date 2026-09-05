using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public class PileProfile
    {
        public Element PileElement { get; set; }
        public XYZ BaseCenter { get; set; }
        public XYZ TopCenter { get; set; }
        public double DiameterMm { get; set; } = 800.0;
        public double LengthMm { get; set; } = 15000.0;
        public double ConcreteCoverMm { get; set; } = 70.0; // Standard underground pile cover
        public double PileHeadEmbedmentMm { get; set; } = 100.0; // Ngàm đầu cọc vào đài
        public double StarterAnchorageLengthMm { get; set; } = 1000.0; // Đoạn râu cọc neo vào đài
        public Element ConnectedPileCap { get; set; }

        public double RadiusMm => DiameterMm / 2.0;
        public double CageRadiusMm(double spiralDiaMm, double mainDiaMm) =>
            Math.Max(50.0, RadiusMm - ConcreteCoverMm - spiralDiaMm - mainDiaMm / 2.0);
    }

    public class PileCageSettings
    {
        /// <summary>
        /// Số lượng thanh thép chủ dọc bố trí đều theo chu vi tròn.
        /// LƯU Ý KỸ THUẬT: Số lượng và đường kính thanh thép chủ được ấn định chính xác theo bảng thống kê
        /// bản vẽ KC-09 (không suy đoán phạm vi 16-24 thanh). Giá trị mặc định là 16 thanh.
        /// </summary>
        public int MainBarCount { get; set; } = 16;
        public double MainBarDiameterMm { get; set; } = 20.0;
        public double SpiralDiameterMm { get; set; } = 10.0;
        public double SpiralPitchHeadMm { get; set; } = 100.0;
        public double SpiralPitchBodyMm { get; set; } = 200.0;
        /// <summary>
        /// Đường kính vành đai định hình (Stiffener ring): D16 (hoặc D20 theo bản vẽ kết cấu KC-09 và tính toán độ cứng cẩu lắp lồng thép).
        /// </summary>
        public double StiffenerDiameterMm { get; set; } = 16.0;
        public double StiffenerSpacingMm { get; set; } = 2000.0;
        public int SonicTestingTubeCount { get; set; } = 3;
        public double SonicTubeDiameterMm { get; set; } = 60.0;
        public bool EnablePileHeadStarterExtension { get; set; } = true;
    }

    /// <summary>
    /// Bộ trích xuất và tính toán hình học lồng thép cọc khoan nhồi (KC-09 Bored Pile D800)
    /// </summary>
    public static class PileGeometryHelper
    {
        public static PileProfile AnalyzePile(Element pile, Element connectedPileCap = null)
        {
            if (pile == null) return null;

            BoundingBoxXYZ bb = pile.get_BoundingBox(null);
            if (bb == null) return null;

            double dx = Math.Abs(bb.Max.X - bb.Min.X);
            double dy = Math.Abs(bb.Max.Y - bb.Min.Y);
            double diaFeet = (dx + dy) / 2.0;
            double lenFeet = Math.Abs(bb.Max.Z - bb.Min.Z);

            XYZ center = (bb.Min + bb.Max) * 0.5;

            return new PileProfile
            {
                PileElement = pile,
                ConnectedPileCap = connectedPileCap,
                DiameterMm = UnitUtils.ConvertFromInternalUnits(diaFeet, UnitTypeId.Millimeters),
                LengthMm = UnitUtils.ConvertFromInternalUnits(lenFeet, UnitTypeId.Millimeters),
                BaseCenter = new XYZ(center.X, center.Y, bb.Min.Z),
                TopCenter = new XYZ(center.X, center.Y, bb.Max.Z),
                ConcreteCoverMm = 70.0
            };
        }
    }
}
