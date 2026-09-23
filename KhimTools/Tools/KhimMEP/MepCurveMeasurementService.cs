using System;
using Autodesk.Revit.DB;
using KhimTools.Core;

namespace KhimTools.MEP
{
    internal sealed class MepCurveSection
    {
        internal double WidthMm { get; }
        internal double HeightMm { get; }
        internal bool IsRound { get; }
        internal string Source { get; }
        internal double MaxDimensionInternal => UnitUtils.ConvertToInternalUnits(Math.Max(WidthMm, HeightMm), UnitTypeId.Millimeters);
        internal MepCurveSection(double widthMm, double heightMm, bool isRound, string source)
        { WidthMm = widthMm; HeightMm = heightMm; IsRound = isRound; Source = source; }
    }

    internal static class MepCurveMeasurementService
    {
        internal static bool IsSupported(MEPCurve curve) => curve?.Category != null &&
            (curve.Category.IsCategory(BuiltInCategory.OST_DuctCurves) ||
             curve.Category.IsCategory(BuiltInCategory.OST_PipeCurves) ||
             curve.Category.IsCategory(BuiltInCategory.OST_CableTray));

        internal static MepCurveSection ReadSection(MEPCurve curve)
        {
            if (!IsSupported(curve)) return null;
            if (curve.Category.IsCategory(BuiltInCategory.OST_PipeCurves))
            {
                Parameter diameter = GetParameter(curve, BuiltInParameter.RBS_PIPE_OUTER_DIAMETER,
                    BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                if (!TryReadLength(diameter, out double internalDiameter)) return null;
                double mm = UnitUtils.ConvertFromInternalUnits(internalDiameter, UnitTypeId.Millimeters);
                return new MepCurveSection(mm, mm, true, "pipe outer diameter");
            }

            bool tray = curve.Category.IsCategory(BuiltInCategory.OST_CableTray);
            BuiltInParameter widthId = tray ? BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM : BuiltInParameter.RBS_CURVE_WIDTH_PARAM;
            BuiltInParameter heightId = tray ? BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM : BuiltInParameter.RBS_CURVE_HEIGHT_PARAM;
            if (!TryReadLength(curve.get_Parameter(widthId), out double internalWidth) ||
                !TryReadLength(curve.get_Parameter(heightId), out double internalHeight)) return null;
            return new MepCurveSection(
                UnitUtils.ConvertFromInternalUnits(internalWidth, UnitTypeId.Millimeters),
                UnitUtils.ConvertFromInternalUnits(internalHeight, UnitTypeId.Millimeters), false,
                tray ? "cable tray width/height" : "duct width/height");
        }

        private static Parameter GetParameter(Element element, params BuiltInParameter[] ids)
        {
            foreach (BuiltInParameter id in ids)
            {
                Parameter parameter = element.get_Parameter(id);
                if (parameter != null && parameter.StorageType == StorageType.Double && parameter.HasValue) return parameter;
            }
            return null;
        }

        private static bool TryReadLength(Parameter parameter, out double value)
        {
            value = 0;
            if (parameter == null || parameter.StorageType != StorageType.Double || !parameter.HasValue) return false;
            value = parameter.AsDouble();
            return value > 1e-9 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
