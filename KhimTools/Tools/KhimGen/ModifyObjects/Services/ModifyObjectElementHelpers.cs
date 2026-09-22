using System;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;
using KhimTools.ParameterTransfer.Services;

namespace KhimTools.ModifyObjects.Services
{
    internal static class ModifyObjectElementHelpers
    {
        public static bool IsVerticalColumn(Element element)
        {
            FamilyInstance instance = element as FamilyInstance;
            return instance != null && element.Category != null && element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns && instance.Location is LocationPoint;
        }
        public static bool IsStraightBeam(Element element)
        {
            FamilyInstance instance = element as FamilyInstance; LocationCurve location = element == null ? null : element.Location as LocationCurve;
            return instance != null && element.Category != null && element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming && location != null && location.Curve is Line;
        }
        public static bool IsLine(Element element) { LocationCurve location = element == null ? null : element.Location as LocationCurve; return location != null && location.Curve is Line; }
        public static bool IsPinnedOrGrouped(Element element) { return element == null || element.Pinned || (element.GroupId != null && element.GroupId != ElementId.InvalidElementId); }
        public static bool TryCopySafeParameters(Element source, Element target, out string conflict)
        {
            conflict = string.Empty; if (source == null || target == null) { conflict = "Missing source or target."; return false; }
            foreach (Parameter sourceParameter in source.Parameters)
            {
                if (sourceParameter == null || sourceParameter.IsReadOnly || ParameterTransferService.IsIdentityParameter(sourceParameter)) continue;
                Parameter targetParameter = ParameterTransferService.FindMatchingParameter(target, ParameterTransferService.CreateKey(sourceParameter));
                if (targetParameter == null || targetParameter.IsReadOnly) continue;
                var outcome = ParameterTransferService.TryApplyValue(targetParameter, ParameterTransferService.Snapshot(sourceParameter), new ParameterTransferOptions { AllowElementId = false, ProtectIdentity = true });
                if (outcome.Status == ParameterTransferStatus.TYPE_MISMATCH || outcome.Status == ParameterTransferStatus.DATA_TYPE_MISMATCH) conflict = sourceParameter.Definition == null ? "Parameter conflict." : sourceParameter.Definition.Name;
            }
            return string.IsNullOrEmpty(conflict);
        }
        public static double Millimeters(double value) { return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters); }
        public static bool IsAlmost(XYZ a, XYZ b, double tolerance = 0.00328084) { return a != null && b != null && a.DistanceTo(b) <= tolerance; }
        public static XYZ Midpoint(Curve curve) { return curve == null ? XYZ.Zero : curve.Evaluate(0.5, true); }
        public static void PreservePointRotation(Document doc, FamilyInstance source, FamilyInstance target)
        {
            LocationPoint sourceLocation = source == null ? null : source.Location as LocationPoint; LocationPoint targetLocation = target == null ? null : target.Location as LocationPoint;
            if (doc == null || sourceLocation == null || targetLocation == null) return;
            double delta = sourceLocation.Rotation - targetLocation.Rotation; if (Math.Abs(delta) < 1e-8) return;
            ElementTransformUtils.RotateElement(doc, target.Id, Line.CreateBound(targetLocation.Point, targetLocation.Point + XYZ.BasisZ), delta);
        }
    }
}
