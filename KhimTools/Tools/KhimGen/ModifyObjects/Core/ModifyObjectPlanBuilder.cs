using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    public static class ModifyObjectPlanBuilder
    {
        public static ModifyObjectPlan Build(ModifyObjectContext context)
        {
            var plan = new ModifyObjectPlan { Context = context };
            if (context == null || context.Document == null) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("Document is unavailable."); return plan; }
            foreach (ElementId id in context.ElementIds.DefaultIfEmpty(context.PrimaryElementId))
            {
                if (id == null || id == ElementId.InvalidElementId) continue;
                Element element = context.Document.GetElement(id);
                if (element == null) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("Element is unavailable: " + id.IntegerValue); continue; }
                plan.Sources.Add(Capture(element));
            }
            if (context.PrimaryElementId != null && context.PrimaryElementId != ElementId.InvalidElementId && plan.Sources.All(s => s.ElementId != context.PrimaryElementId)) plan.Sources.Add(Capture(context.Document.GetElement(context.PrimaryElementId)));
            if (plan.Sources.Count == 0) { plan.Status = ModifyObjectStatus.INVALID_SELECTION; plan.Errors.Add("At least one model element is required."); }
            plan.Fingerprint = string.Join("|", plan.Sources.Select(s => s.GeometryFingerprint));
            return plan;
        }
        public static ModifyObjectSourceSnapshot Capture(Element element)
        {
            FamilyInstance familyInstance = element as FamilyInstance; DesignOption option = element == null ? null : element.DesignOption;
            return new ModifyObjectSourceSnapshot { ElementId = element == null ? ElementId.InvalidElementId : element.Id, UniqueId = element == null ? string.Empty : element.UniqueId, CategoryId = element == null || element.Category == null ? ElementId.InvalidElementId : element.Category.Id, TypeId = element == null ? ElementId.InvalidElementId : element.GetTypeId(), LevelId = element == null ? ElementId.InvalidElementId : element.LevelId, BaseOffset = GetOffset(element, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM), TopOffset = GetOffset(element, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM), Location = GetPoint(element), GeometryFingerprint = BuildFingerprint(element), ParameterFingerprint = BuildParameterFingerprint(element), GroupId = element == null ? ElementId.InvalidElementId : element.GroupId, DesignOptionId = option == null ? ElementId.InvalidElementId : option.Id, Pinned = element != null && element.Pinned, HostRelation = familyInstance == null || familyInstance.Host == null ? string.Empty : familyInstance.Host.UniqueId, CapturedAt = DateTime.UtcNow };
        }
        public static string BuildFingerprint(Element element)
        {
            if (element == null) return string.Empty;
            BoundingBoxXYZ bb = null; try { bb = element.get_BoundingBox(null); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ModifyObjects] bbox: " + ex.Message); }
            LocationCurve lc = element.Location as LocationCurve; string curve = lc == null || lc.Curve == null ? string.Empty : lc.Curve.GetEndPoint(0).ToString() + ";" + lc.Curve.GetEndPoint(1).ToString();
            LocationPoint lp = element.Location as LocationPoint; string point = lp == null || lp.Point == null ? string.Empty : lp.Point.ToString();
            return element.Id.IntegerValue + ":" + element.GetTypeId().IntegerValue + ":" + curve + ":" + point + ":" + (bb == null ? string.Empty : bb.Min + ";" + bb.Max);
        }
        private static XYZ GetPoint(Element element) { LocationPoint p = element == null ? null : element.Location as LocationPoint; return p == null ? XYZ.Zero : p.Point; }
        private static double GetOffset(Element element, BuiltInParameter parameter) { if (element == null) return 0; try { Parameter value = element.get_Parameter(parameter); return value == null || value.StorageType != StorageType.Double ? 0 : value.AsDouble(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[ModifyObjects] offset: " + ex.Message); return 0; } }
        private static string BuildParameterFingerprint(Element element) { if (element == null) return string.Empty; return string.Join(";", element.Parameters.Cast<Parameter>().Where(p => p != null && p.Definition != null).OrderBy(p => p.Definition.Name).Take(32).Select(p => p.Definition.Name + "=" + p.StorageType)); }
    }
}
