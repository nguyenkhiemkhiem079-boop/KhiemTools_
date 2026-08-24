using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.ElementTags.Models;

namespace KhimTools.ElementTags.Services
{
    public static class ElementTagsService
    {
        public static readonly Dictionary<BuiltInCategory, BuiltInCategory> HostToTagCategoryMap = new Dictionary<BuiltInCategory, BuiltInCategory>
        {
            { BuiltInCategory.OST_Walls, BuiltInCategory.OST_WallTags },
            { BuiltInCategory.OST_Floors, BuiltInCategory.OST_FloorTags },
            { BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralColumnTags },
            { BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralFramingTags },
            { BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_GenericModelTags },
            { BuiltInCategory.OST_DetailComponents, BuiltInCategory.OST_DetailComponentTags },
            { BuiltInCategory.OST_Windows, BuiltInCategory.OST_WindowTags },
            { BuiltInCategory.OST_Doors, BuiltInCategory.OST_DoorTags },
            { BuiltInCategory.OST_Stairs, BuiltInCategory.OST_StairsTags },
            { BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_CeilingTags },
            { BuiltInCategory.OST_Roofs, BuiltInCategory.OST_RoofTags },
            { BuiltInCategory.OST_StairsRailing, BuiltInCategory.OST_RailingSystemTags },
            { BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureTags },
            { BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_MechanicalEquipmentTags },
            { BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_DuctTags },
            { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_PipeTags },
            { BuiltInCategory.OST_CableTray, BuiltInCategory.OST_CableTrayTags }
        };

        public static List<ElementTagsItem> GetTaggableCategoriesInView(Document doc, View view)
        {
            var result = new List<ElementTagsItem>();

            // Collect all loaded tag family symbols
            var allTagSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null)
                .ToList();

            foreach (var kvp in HostToTagCategoryMap)
            {
                // Check if category has elements in active view
                var hostCol = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(kvp.Key)
                    .WhereElementIsNotElementType();

                if (!hostCol.Any()) continue;

                // Find symbols matching tag category
                var matchedSymbols = allTagSymbols
                    .Where(fs => fs.Category.IsCategory(kvp.Value))
                    .OrderBy(fs => fs.Family.Name)
                    .ThenBy(fs => fs.Name)
                    .ToList();

                if (!matchedSymbols.Any()) continue;

                string catName = doc.Settings.Categories.get_Item(kvp.Key)?.Name ?? kvp.Key.ToString().Replace("OST_", "");

                result.Add(new ElementTagsItem
                {
                    IsChecked = true,
                    CategoryName = catName,
                    HostCategory = kvp.Key,
                    TagCategory = kvp.Value,
                    AvailableTagSymbols = matchedSymbols,
                    SelectedTagSymbol = matchedSymbols.FirstOrDefault()
                });
            }

            return result;
        }

        public static int TagElements(Document doc, View view, List<ElementTagsItem> configs, bool addLeader, bool onlyUntagged, List<ElementId> preSelectedIds)
        {
            int createdCount = 0;

            // Collect existing tags to avoid duplicate tagging if checked
            var existingTags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            var taggedHostsSet = new HashSet<ElementId>();
            if (onlyUntagged)
            {
                foreach (var tag in existingTags)
                {
                    ElementId hostId = GetTaggedElementId(tag);
                    if (hostId != null && hostId != ElementId.InvalidElementId)
                    {
                        taggedHostsSet.Add(hostId);
                    }
                }
            }

            using (var tx = new Transaction(doc, "K-TOOLS: Elements Auto Tag"))
            {
                tx.Start();

                foreach (var config in configs)
                {
                    if (!config.IsChecked || config.SelectedTagSymbol == null) continue;

                    // Collect target host elements
                    var hostCollector = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(config.HostCategory)
                        .WhereElementIsNotElementType();

                    var hosts = hostCollector.ToList();
                    if (preSelectedIds != null && preSelectedIds.Any())
                    {
                        hosts = hosts.Where(h => preSelectedIds.Contains(h.Id)).ToList();
                    }

                    foreach (var host in hosts)
                    {
                        if (onlyUntagged && taggedHostsSet.Contains(host.Id)) continue;

                        try
                        {
                            // Calculate position
                            XYZ point = GetElementTagPoint(host, view);

                            // Place tag
                            Reference reference = new Reference(host);
#if NET48
                            IndependentTag newTag = IndependentTag.Create(
                                doc, config.SelectedTagSymbol.Id, view.Id, reference,
                                addLeader, TagOrientation.Horizontal, point);
#else
                            IndependentTag newTag = IndependentTag.Create(
                                doc, config.SelectedTagSymbol.Id, view.Id, reference,
                                addLeader, TagOrientation.Horizontal, point);
#endif
                            if (newTag != null)
                            {
                                createdCount++;
                            }
                        }
                        catch { }
                    }
                }

                tx.Commit();
            }

            return createdCount;
        }

        public static void ApplyColorOverride(Document doc, View view, List<ElementTagsItem> configs)
        {
            using (var tx = new Transaction(doc, "K-TOOLS: Tag Color Overrides"))
            {
                tx.Start();

                foreach (var config in configs)
                {
                    var tagCat = doc.Settings.Categories.get_Item(config.TagCategory);
                    if (tagCat == null) continue;

                    var ogs = new OverrideGraphicSettings();
                    var dbColor = new Autodesk.Revit.DB.Color(config.TagColor.R, config.TagColor.G, config.TagColor.B);

                    // Set color override
                    ogs.SetProjectionLineColor(dbColor);

                    view.SetCategoryOverrides(tagCat.Id, ogs);
                }

                tx.Commit();
            }
        }

        public static void CheckTagsStatus(
            Document doc,
            View view,
            out List<ElementId> orphanTagIds,
            out List<ElementId> invisibleHostTagIds,
            out List<ElementId> tooFarTagIds)
        {
            orphanTagIds = new List<ElementId>();
            invisibleHostTagIds = new List<ElementId>();
            tooFarTagIds = new List<ElementId>();

            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            // Collect all elements currently visible in view to check if host is visible
            var visibleElementIds = new FilteredElementCollector(doc, view.Id)
                .ToElementIds()
                .Select(id => id.ToLongValue())
                .ToHashSet();

            foreach (var tag in tags)
            {
                ElementId hostId = GetTaggedElementId(tag);
                if (hostId == null || hostId == ElementId.InvalidElementId)
                {
                    orphanTagIds.Add(tag.Id);
                    continue;
                }

                Element host = doc.GetElement(hostId);
                if (host == null)
                {
                    orphanTagIds.Add(tag.Id);
                    continue;
                }

                // Check 1: Host is hidden/invisible in the active view
                if (!visibleElementIds.Contains(hostId.ToLongValue()))
                {
                    invisibleHostTagIds.Add(tag.Id);
                }

                // Check 2: Tag is too far from host (Nằm đúng vị trí thể hiện chưa)
                try
                {
                    XYZ hostPoint = GetElementTagPoint(host, view);
                    XYZ tagPoint = tag.TagHeadPosition;
                    double dist = tagPoint.DistanceTo(hostPoint);

                    // If tag is too far (e.g. more than 6.5 feet / 2 meters) and has no leader, or has leader but extremely far (> 20 feet / 6 meters)
                    bool hasLeader = tag.HasLeader;
                    double threshold = hasLeader ? 20.0 : 6.5; // In internal Revit feet
                    if (dist > threshold)
                    {
                        tooFarTagIds.Add(tag.Id);
                    }
                }
                catch { }
            }
        }

        public static int ResolveClashingTags(Document doc, View view)
        {
            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            int adjustedCount = 0;

            using (var tx = new Transaction(doc, "K-TOOLS: Clash Tag Adjuster"))
            {
                tx.Start();

                for (int i = 0; i < tags.Count; i++)
                {
                    var tagA = tags[i];
                    var boxA = tagA.get_BoundingBox(view);
                    if (boxA == null) continue;

                    for (int j = i + 1; j < tags.Count; j++)
                    {
                        var tagB = tags[j];
                        var boxB = tagB.get_BoundingBox(view);
                        if (boxB == null) continue;

                        // Check intersection
                        if (boxA.Min.X < boxB.Max.X && boxA.Max.X > boxB.Min.X &&
                            boxA.Min.Y < boxB.Max.Y && boxA.Max.Y > boxB.Min.Y)
                        {
                            // Shift Tag B slightly on Y-axis
                            try
                            {
                                XYZ oldHead = tagB.TagHeadPosition;
                                tagB.TagHeadPosition = new XYZ(oldHead.X, oldHead.Y + 0.5, oldHead.Z);
                                adjustedCount++;
                            }
                            catch { }
                        }
                    }
                }

                tx.Commit();
            }

            return adjustedCount;
        }

        private static XYZ GetElementTagPoint(Element el, View view)
        {
            if (el.Location is LocationPoint lp)
            {
                return lp.Point;
            }
            if (el.Location is LocationCurve lc)
            {
                return lc.Curve.Evaluate(0.5, true);
            }

            var bbox = el.get_BoundingBox(view);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) * 0.5;
            }

            return XYZ.Zero;
        }

        private static ElementId GetTaggedElementId(IndependentTag tag)
        {
            try
            {
                var method = tag.GetType().GetMethod("GetTaggedLocalElementIds");
                if (method != null)
                {
                    var ids = method.Invoke(tag, null) as System.Collections.IEnumerable;
                    if (ids != null)
                    {
                        foreach (var linkId in ids)
                        {
                            var hostIdProp = linkId.GetType().GetProperty("HostElementId");
                            if (hostIdProp != null)
                            {
                                return hostIdProp.GetValue(linkId) as ElementId;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var prop = tag.GetType().GetProperty("TaggedLocalElementId");
                if (prop != null)
                {
                    return prop.GetValue(tag) as ElementId;
                }
            }
            catch { }

            return ElementId.InvalidElementId;
        }

        public static bool ResetFloorTagHost(Document doc, View view, IndependentTag tag, out string successFloorName)
        {
            successFloorName = string.Empty;
            XYZ tagPoint = tag.TagHeadPosition;

            // Collect all floors visible in the view
            var visibleFloors = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .ToList();

            Floor targetFloor = null;

            foreach (var floor in visibleFloors)
            {
                try
                {
                    var topFaceRefs = HostObjectUtils.GetTopFaces(floor);
                    foreach (var faceRef in topFaceRefs)
                    {
                        var face = floor.GetGeometryObjectFromReference(faceRef) as PlanarFace;
                        if (face != null)
                        {
                            var proj = face.Project(tagPoint);
                            if (proj != null)
                            {
                                targetFloor = floor;
                                break;
                            }
                        }
                    }
                }
                catch { }

                if (targetFloor != null)
                    break;
            }

            if (targetFloor == null)
            {
                return false;
            }

            // Perform re-host in transaction using reflection for target safety
            using (var tx = new Transaction(doc, "K-TOOLS: Reset Floor Host"))
            {
                tx.Start();

                try
                {
                    bool rehostDone = false;

                    // Try Revit 2023+ SetTaggedReferences method
                    var setRefsMethod = tag.GetType().GetMethod("SetTaggedReferences");
                    if (setRefsMethod != null)
                    {
                        var refsList = new List<Reference> { new Reference(targetFloor) };
                        setRefsMethod.Invoke(tag, new object[] { refsList });
                        rehostDone = true;
                    }

                    // Try Revit 2022 and older ChangeLocalElementBind method
                    if (!rehostDone)
                    {
                        var changeBindMethod = tag.GetType().GetMethod("ChangeLocalElementBind", new Type[] { typeof(ElementId) });
                        if (changeBindMethod != null)
                        {
                            changeBindMethod.Invoke(tag, new object[] { targetFloor.Id });
                            rehostDone = true;
                        }
                    }

                    // Try setting TaggedLocalElementId property
                    if (!rehostDone)
                    {
                        var prop = tag.GetType().GetProperty("TaggedLocalElementId");
                        if (prop != null)
                        {
                            prop.SetValue(tag, targetFloor.Id);
                            rehostDone = true;
                        }
                    }

                    if (rehostDone)
                    {
                        successFloorName = $"{targetFloor.Name} (ID: {targetFloor.Id})";
                        tx.Commit();
                        return true;
                    }
                    else
                    {
                        tx.RollBack();
                    }
                }
                catch
                {
                    tx.RollBack();
                }
            }

            return false;
        }
    }
}
