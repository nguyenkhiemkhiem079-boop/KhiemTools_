using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;

namespace KhimTools.ModifyObjects.Core
{
    /// <summary>Operation-specific end-state checks. These run against the live document inside the outer transaction group.</summary>
    public static class ModifyObjectPostconditionVerifier
    {
        public static bool Verify(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string requirement, out string message)
        {
            requirement = Requirement(plan == null || plan.Context == null ? ModifyObjectOperation.MOVE_3D : plan.Context.Operation);
            message = string.Empty;
            if (doc == null || plan == null || plan.Context == null || result == null) { message = "Postcondition inputs are unavailable."; return false; }
            if (plan.Context.Operation == ModifyObjectOperation.SLAB_SPLIT)
            { message = "SLAB_SPLIT is a capability-only operation and must not mutate the model."; return result.Status == ModifyObjectStatus.SLAB_SPLIT_CAPABILITY_UNSUPPORTED; }

            bool outputsResolve = result.CreatedElementIds.All(id => id != null && id != ElementId.InvalidElementId && doc.GetElement(id) != null);
            if (!outputsResolve) { message = "One or more reported created element IDs do not resolve."; return false; }
            switch (plan.Context.Operation)
            {
                case ModifyObjectOperation.MOVE_3D: return VerifyMove(doc, plan, out message);
                case ModifyObjectOperation.ARRAY_3D: return VerifyArray(doc, plan, result, out message);
                case ModifyObjectOperation.CREATE_PARTS: return VerifyParts(doc, plan, result, out message);
                case ModifyObjectOperation.COLUMN_SPLIT: return VerifySplit(doc, plan, result, BuiltInCategory.OST_StructuralColumns, out message);
                case ModifyObjectOperation.COLUMN_JOIN: return VerifyColumnJoin(doc, plan, result, out message);
                case ModifyObjectOperation.BEAM_SPLIT: return VerifySplit(doc, plan, result, BuiltInCategory.OST_StructuralFraming, out message);
                case ModifyObjectOperation.BEAM_JOIN: return VerifyBeamJoin(doc, plan, result, out message);
                case ModifyObjectOperation.SLAB_JOIN: return VerifySlabJoin(doc, plan, out message);
                case ModifyObjectOperation.WALL_SPLIT: return VerifySplit(doc, plan, result, BuiltInCategory.OST_Walls, out message);
                case ModifyObjectOperation.WALL_TRIM: return VerifyWallTrim(doc, plan, out message);
                case ModifyObjectOperation.WALL_OPENING: return VerifyOpening(doc, plan, result, out message);
                case ModifyObjectOperation.COLUMN_BASE_ELEVATION: return VerifyColumnBase(doc, plan, out message);
                default: message = "No postcondition is defined for this Modify Objects operation."; return false;
            }
        }

        public static string Requirement(ModifyObjectOperation operation)
        {
            switch (operation)
            {
                case ModifyObjectOperation.MOVE_3D: return "Every selected source remains and its point/curve location equals its captured location plus the requested vector.";
                case ModifyObjectOperation.ARRAY_3D: return "Source remains; at least count-1 reported copies resolve and their IDs are distinct.";
                case ModifyObjectOperation.CREATE_PARTS: return "Every reported Part resolves and is associated with one of the requested source elements.";
                case ModifyObjectOperation.COLUMN_SPLIT: return "Original column is deleted; exactly two structural columns resolve with contiguous base/top level constraints at the requested split level.";
                case ModifyObjectOperation.COLUMN_JOIN: return "Exactly one input column is deleted; survivor resolves and carries the removed column's top level constraint.";
                case ModifyObjectOperation.BEAM_SPLIT: return "Original beam is deleted; exactly two framing curves resolve, meet at the requested split point, and preserve total length.";
                case ModifyObjectOperation.BEAM_JOIN: return "Second beam is deleted; first remains with a straight curve whose length equals the input lengths.";
                case ModifyObjectOperation.SLAB_JOIN: return "Revit reports the two input floors joined by JoinGeometryUtils.";
                case ModifyObjectOperation.SLAB_SPLIT: return "Capability-only refusal; no element is created, modified, or deleted.";
                case ModifyObjectOperation.WALL_SPLIT: return "Original wall is deleted; exactly two wall curves meet at the split point and preserve total length.";
                case ModifyObjectOperation.WALL_TRIM: return "Wall remains; its line endpoint matches the projected trim point and its length decreases.";
                case ModifyObjectOperation.WALL_OPENING: return "Exactly one reported Opening resolves and lies within the wall host bounds.";
                case ModifyObjectOperation.COLUMN_BASE_ELEVATION: return "Structural column base level and offset equal the requested values after regeneration.";
                default: return "No acceptance rule registered.";
            }
        }

        private static bool VerifyMove(Document doc, ModifyObjectPlan plan, out string message)
        {
            XYZ delta = plan.Context.MoveVector == null ? null : plan.Context.MoveVector.ToXyz();
            if (delta == null) { message = "Requested move vector is unavailable."; return false; }
            foreach (ModifyObjectSourceSnapshot source in plan.Sources)
            {
                Element element = doc.GetElement(source.ElementId);
                if (element == null) { message = "A selected source disappeared during Move 3D."; return false; }
                LocationPoint point = element.Location as LocationPoint;
                LocationCurve curve = element.Location as LocationCurve;
                if (point != null)
                {
                    if (source.LocationPoint == null || !point.Point.IsAlmostEqualTo(source.LocationPoint.ToXyz() + delta)) { message = "A moved point location differs from the captured location plus requested vector."; return false; }
                }
                else if (curve != null)
                {
                    if (source.CurveStart == null || source.CurveEnd == null ||
                        !curve.Curve.GetEndPoint(0).IsAlmostEqualTo(source.CurveStart.ToXyz() + delta) || !curve.Curve.GetEndPoint(1).IsAlmostEqualTo(source.CurveEnd.ToXyz() + delta))
                    { message = "A moved curve differs from the captured curve plus requested vector."; return false; }
                }
                else
                {
                    BoundingBoxXYZ box = element.get_BoundingBox(null);
                    if (box == null || source.BoundingBoxCenter == null || !((box.Min + box.Max) / 2.0).IsAlmostEqualTo(source.BoundingBoxCenter.ToXyz() + delta))
                    { message = "A moved element has no verifiable location/bounding-box translation."; return false; }
                }
            }
            message = "Every supported point/curve source matches the requested translation."; return true;
        }

        private static bool VerifyArray(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string message)
        {
            int minimum = Math.Max(0, plan.Context.ArrayCount - 1);
            bool valid = doc.GetElement(plan.Context.PrimaryElementId) != null && result.CreatedElementIds.Distinct().Count() == result.CreatedElementIds.Count && result.CreatedElementIds.Count >= minimum;
            message = valid ? "Original retained and distinct copies resolve (minimum=" + minimum + ")." : "Array did not produce the requested minimum copies or repeated an element ID."; return valid;
        }

        private static bool VerifyParts(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string message)
        {
            if (result.CreatedElementIds.Count == 0) { message = "No Parts were created; PARTIAL cannot be reported as success."; return false; }
            HashSet<ElementId> sources = new HashSet<ElementId>(plan.Context.ElementIds);
            foreach (ElementId id in result.CreatedElementIds)
            {
                Part part = doc.GetElement(id) as Part;
                if (part == null || part.GetSourceElementIds().All(sourceId => !sources.Contains(sourceId.HostElementId))) { message = "A reported Part is not associated with the requested sources."; return false; }
            }
            message = "Every reported Part resolves and has a requested source."; return true;
        }

        private static bool VerifySplit(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, BuiltInCategory category, out string message)
        {
            if (doc.GetElement(plan.Context.PrimaryElementId) != null || result.CreatedElementIds.Count != 2 || result.DeletedSourceIds.Count != 1)
            { message = "Split must delete its source and report exactly two replacements."; return false; }
            Element[] elements = result.CreatedElementIds.Select(id => doc.GetElement(id)).ToArray();
            if (elements.Any(element => element.Category == null || !element.Category.IsCategory(category))) { message = "Replacement category does not match the requested split operation."; return false; }
            if (category == BuiltInCategory.OST_StructuralColumns)
            {
                FamilyInstance[] columns = elements.Cast<FamilyInstance>().ToArray();
                ModifyObjectSourceSnapshot source = plan.Sources.FirstOrDefault(item => item.ElementId == plan.Context.PrimaryElementId);
                FamilyInstance lower = columns.SingleOrDefault(column => { Parameter p = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM); return p != null && p.AsElementId() == plan.Context.TargetLevelId; });
                FamilyInstance upper = columns.SingleOrDefault(column => { Parameter p = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM); return p != null && p.AsElementId() == plan.Context.TargetLevelId; });
                Parameter lowerBase = lower == null ? null : lower.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                Parameter upperTop = upper == null ? null : upper.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                if (source == null || lower == null || upper == null || lower == upper || lowerBase == null || upperTop == null ||
                    lowerBase.AsElementId() != source.LevelId || upperTop.AsElementId() != source.TopLevelId)
                { message = "Column replacements do not preserve the original base/top constraints while meeting at the requested split Level."; return false; }
            }
            else
            {
                LocationCurve[] locations = elements.Select(element => element.Location as LocationCurve).ToArray();
                if (locations.Any(location => location == null || !(location.Curve is Line))) { message = "A split replacement is missing a straight LocationCurve."; return false; }
                double length = locations.Sum(location => location.Curve.Length);
                XYZ split = plan.Context.SplitPoint == null ? null : plan.Context.SplitPoint.ToXyz();
                if (split == null || locations.All(location => location.Curve.GetEndPoint(0).DistanceTo(split) > 1e-4 && location.Curve.GetEndPoint(1).DistanceTo(split) > 1e-4)) { message = "Neither replacement curve meets the requested split point."; return false; }
                ModifyObjectSourceSnapshot source = plan.Sources.FirstOrDefault(item => item.ElementId == plan.Context.PrimaryElementId);
                if (source != null && source.CurveLength > 0 && Math.Abs(length - source.CurveLength) > 1e-4) { message = "Replacement curve lengths do not preserve source length."; return false; }
            }
            message = "Source replaced by two operation-appropriate segments with checked split relationship."; return true;
        }

        private static bool VerifyColumnJoin(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string message)
        {
            ElementId survivorId = plan.Context.ConflictPolicy == ModifyObjectConflictPolicy.KEEP_UPPER ? plan.Context.SecondaryElementId : plan.Context.PrimaryElementId;
            ElementId removedId = survivorId == plan.Context.PrimaryElementId ? plan.Context.SecondaryElementId : plan.Context.PrimaryElementId;
            FamilyInstance survivor = doc.GetElement(survivorId) as FamilyInstance;
            ModifyObjectSourceSnapshot removed = plan.Sources.FirstOrDefault(source => source.ElementId == removedId);
            Parameter top = survivor == null ? null : survivor.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
            bool valid = survivor != null && doc.GetElement(removedId) == null && result.DeletedSourceIds.Contains(removedId) && removed != null && top != null && top.AsElementId() == removed.TopLevelId;
            message = valid ? "Chosen survivor remains, other column is deleted, and top constraint is transferred." : "Column survivor/deletion/top-constraint postcondition failed."; return valid;
        }

        private static bool VerifyBeamJoin(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string message)
        {
            FamilyInstance survivor = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance;
            Element second = doc.GetElement(plan.Context.SecondaryElementId);
            LocationCurve curve = survivor == null ? null : survivor.Location as LocationCurve;
            ModifyObjectSourceSnapshot a = plan.Sources.FirstOrDefault(source => source.ElementId == plan.Context.PrimaryElementId);
            ModifyObjectSourceSnapshot b = plan.Sources.FirstOrDefault(source => source.ElementId == plan.Context.SecondaryElementId);
            bool valid = survivor != null && second == null && result.DeletedSourceIds.Contains(plan.Context.SecondaryElementId) && curve != null && curve.Curve is Line && a != null && b != null && Math.Abs(curve.Curve.Length - a.CurveLength - b.CurveLength) <= 1e-4;
            message = valid ? "Primary beam remains straight with combined input length; second is deleted." : "Beam join geometry/deletion postcondition failed."; return valid;
        }

        private static bool VerifySlabJoin(Document doc, ModifyObjectPlan plan, out string message)
        {
            Element first = doc.GetElement(plan.Context.PrimaryElementId), second = doc.GetElement(plan.Context.SecondaryElementId);
            bool valid = first is Floor && second is Floor && JoinGeometryUtils.AreElementsJoined(doc, first, second);
            message = valid ? "JoinGeometryUtils confirms the floor pair is joined." : "Revit does not report the floor pair joined."; return valid;
        }

        private static bool VerifyWallTrim(Document doc, ModifyObjectPlan plan, out string message)
        {
            Element wall = doc.GetElement(plan.Context.PrimaryElementId);
            LocationCurve location = wall == null ? null : wall.Location as LocationCurve;
            XYZ point = plan.Context.SplitPoint == null ? null : plan.Context.SplitPoint.ToXyz();
            if (location == null || !(location.Curve is Line) || point == null) { message = "Trimmed wall or requested trim point is unavailable."; return false; }
            XYZ projected = location.Curve.Project(point).XYZPoint;
            ModifyObjectSourceSnapshot source = plan.Sources.FirstOrDefault(item => item.ElementId == plan.Context.PrimaryElementId);
            bool endpoint = (location.Curve.GetEndPoint(0).DistanceTo(projected) <= 1e-4 || location.Curve.GetEndPoint(1).DistanceTo(projected) <= 1e-4) && source != null && location.Curve.Length < source.CurveLength - 1e-4;
            message = endpoint ? "Wall endpoint matches the projected trim point and length decreased." : "Trimmed wall endpoint/length postcondition failed."; return endpoint;
        }

        private static bool VerifyOpening(Document doc, ModifyObjectPlan plan, ModifyObjectResult result, out string message)
        {
            Opening opening = result.CreatedElementIds.Count == 1 ? doc.GetElement(result.CreatedElementIds[0]) as Opening : null;
            Element host = doc.GetElement(plan.Context.PrimaryElementId);
            BoundingBoxXYZ openingBox = opening == null ? null : opening.get_BoundingBox(null);
            BoundingBoxXYZ hostBox = host == null ? null : host.get_BoundingBox(null);
            bool valid = opening != null && openingBox != null && hostBox != null &&
                openingBox.Min.X >= hostBox.Min.X - 1e-4 && openingBox.Max.X <= hostBox.Max.X + 1e-4 &&
                openingBox.Min.Y >= hostBox.Min.Y - 1e-4 && openingBox.Max.Y <= hostBox.Max.Y + 1e-4 &&
                openingBox.Min.Z >= hostBox.Min.Z - 1e-4 && openingBox.Max.Z <= hostBox.Max.Z + 1e-4;
            message = valid ? "Exactly one Opening resolves within the host wall bounds." : "Wall opening count/host-bound postcondition failed."; return valid;
        }

        private static bool VerifyColumnBase(Document doc, ModifyObjectPlan plan, out string message)
        {
            FamilyInstance column = doc.GetElement(plan.Context.PrimaryElementId) as FamilyInstance;
            Parameter level = column == null ? null : column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
            Parameter offset = column == null ? null : column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
            bool valid = level != null && offset != null && level.AsElementId() == plan.Context.TargetLevelId && Math.Abs(offset.AsDouble() - plan.Context.TargetBaseOffset) <= 1e-8;
            message = valid ? "Base level and offset equal the requested values." : "Column base level/offset postcondition failed."; return valid;
        }
    }
}
