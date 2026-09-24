using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Workflow;

namespace KhimTools.ModifyObjects.Core
{
    public static class ModifyObjectPlanBuilder
    {
        public static ModifyObjectPlan Build(ModifyObjectContext context)
        {
            var plan = new ModifyObjectPlan { Context = ModifyObjectPlanContext.From(context) };
            if (context == null || context.Document == null) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("Document is unavailable."); return plan; }
            foreach (ElementId id in context.ElementIds.DefaultIfEmpty(context.PrimaryElementId))
            {
                if (id == null || id == ElementId.InvalidElementId) continue;
                Element element = context.Document.GetElement(id);
                if (element == null) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("Element is unavailable: " + id.ToLongValue()); continue; }
                plan.Sources.Add(Capture(element));
            }
            if (context.PrimaryElementId != null && context.PrimaryElementId != ElementId.InvalidElementId && plan.Sources.All(s => s.ElementId != context.PrimaryElementId)) plan.Sources.Add(Capture(context.Document.GetElement(context.PrimaryElementId)));
            if (plan.Sources.Count == 0) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("At least one model element is required."); }
            plan.Fingerprint = WorkflowFingerprint.Compute(new[] { ContextFingerprint(plan.Context) }.Concat(plan.Sources.OrderBy(source => source.UniqueId, System.StringComparer.Ordinal).Select(source => source.UniqueId + "|" + source.GeometryFingerprint + "|" + source.ParameterFingerprint)));
            return plan;
        }

        public static string ContextFingerprint(ModifyObjectPlanContext context)
        {
            if (context == null) return WorkflowFingerprint.Compute("<no-context>");
            string Point(ModifyObjectPointSnapshot point) => point == null ? string.Empty :
                point.X.ToString("R", CultureInfo.InvariantCulture) + "," +
                point.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
                point.Z.ToString("R", CultureInfo.InvariantCulture);
            string Id(ElementId id) => id == null ? string.Empty : id.ToLongValue().ToString(CultureInfo.InvariantCulture);
            return WorkflowFingerprint.Compute(new[]
            {
                context.DocumentIdentityKey ?? string.Empty, context.Operation.ToString(),
                string.Join("|", (context.ElementIds ?? new List<ElementId>()).Select(Id).OrderBy(value => value, StringComparer.Ordinal)),
                Id(context.PrimaryElementId), Id(context.SecondaryElementId),
                Point(context.SplitPoint), Point(context.OpeningStart), Point(context.OpeningEnd),
                Point(context.MoveVector), context.ArrayCount.ToString(CultureInfo.InvariantCulture), Point(context.ArrayVector),
                Id(context.TargetLevelId), context.TargetBaseOffset.ToString("R", CultureInfo.InvariantCulture),
                context.IncludeOriginal.ToString(), context.PreviewOnly.ToString(), context.ConflictPolicy.ToString()
            });
        }
        public static ModifyObjectSourceSnapshot Capture(Element element)
        {
            FamilyInstance familyInstance = element as FamilyInstance; DesignOption option = element == null ? null : element.DesignOption;
            LocationPoint point = element == null ? null : element.Location as LocationPoint;
            LocationCurve locationCurve = element == null ? null : element.Location as LocationCurve;
            Parameter top = element == null ? null : element.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
            BoundingBoxXYZ box = null; try { box = element == null ? null : element.get_BoundingBox(null); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ModifyObjects] snapshot bbox: " + ex.Message); }
            XYZ center = box == null ? null : (box.Min + box.Max) / 2.0;
            return new ModifyObjectSourceSnapshot { ElementId = element == null ? ElementId.InvalidElementId : element.Id, UniqueId = element == null ? string.Empty : element.UniqueId, CategoryId = element == null || element.Category == null ? ElementId.InvalidElementId : element.Category.Id, TypeId = element == null ? ElementId.InvalidElementId : element.GetTypeId(), LevelId = element == null ? ElementId.InvalidElementId : element.LevelId, TopLevelId = top == null || top.StorageType != StorageType.ElementId ? ElementId.InvalidElementId : top.AsElementId(), LocationPoint = point == null ? null : ModifyObjectPointSnapshot.From(point.Point), BoundingBoxCenter = center == null ? null : ModifyObjectPointSnapshot.From(center), CurveStart = locationCurve == null || locationCurve.Curve == null ? null : ModifyObjectPointSnapshot.From(locationCurve.Curve.GetEndPoint(0)), CurveEnd = locationCurve == null || locationCurve.Curve == null ? null : ModifyObjectPointSnapshot.From(locationCurve.Curve.GetEndPoint(1)), CurveLength = locationCurve == null || locationCurve.Curve == null ? 0 : locationCurve.Curve.Length, BaseOffset = GetOffset(element, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM), TopOffset = GetOffset(element, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM), GeometryFingerprint = BuildFingerprint(element), ParameterFingerprint = BuildParameterFingerprint(element), GroupId = element == null ? ElementId.InvalidElementId : element.GroupId, DesignOptionId = option == null ? ElementId.InvalidElementId : option.Id, Pinned = element != null && element.Pinned, HostRelation = familyInstance == null || familyInstance.Host == null ? string.Empty : familyInstance.Host.UniqueId, CapturedAt = DateTime.UtcNow };
        }
        public static string BuildFingerprint(Element element)
        {
            if (element == null) return string.Empty;
            BoundingBoxXYZ bb = null; try { bb = element.get_BoundingBox(null); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ModifyObjects] bbox: " + ex.Message); }
            LocationCurve lc = element.Location as LocationCurve; string curve = lc == null || lc.Curve == null ? string.Empty : lc.Curve.GetEndPoint(0).ToString() + ";" + lc.Curve.GetEndPoint(1).ToString();
            LocationPoint lp = element.Location as LocationPoint; string point = lp == null || lp.Point == null ? string.Empty : lp.Point.ToString();
            return element.Id.ToLongValue() + ":" + element.GetTypeId().ToLongValue() + ":" + curve + ":" + point + ":" + (bb == null ? string.Empty : bb.Min + ";" + bb.Max);
        }
        private static double GetOffset(Element element, BuiltInParameter parameter) { if (element == null) return 0; try { Parameter value = element.get_Parameter(parameter); return value == null || value.StorageType != StorageType.Double ? 0 : value.AsDouble(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ModifyObjects] offset: " + ex.Message); return 0; } }
        private static string BuildParameterFingerprint(Element element) { if (element == null) return string.Empty; return string.Join(";", element.Parameters.Cast<Parameter>().Where(p => p != null && p.Definition != null).OrderBy(p => p.Definition.Name).Take(32).Select(p => p.Definition.Name + "=" + p.StorageType)); }
    }
}
