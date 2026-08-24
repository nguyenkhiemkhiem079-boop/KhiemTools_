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
            out List<ElementId> tooFarTagIds,
            out List<ElementId> clashingTagIds)
        {
            orphanTagIds = new List<ElementId>();
            invisibleHostTagIds = new List<ElementId>();
            tooFarTagIds = new List<ElementId>();
            clashingTagIds = new List<ElementId>();

            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            // Collect all elements currently visible in view to check if host is visible
            var visibleElementIds = new FilteredElementCollector(doc, view.Id)
                .ToElementIds()
                .Select(id => id.ToLongValue())
                .ToHashSet();

            // Collect structural elements to check clash against
            var structuralCategories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_StairsRailing
            };

            var structuralElements = new List<Element>();
            foreach (var cat in structuralCategories)
            {
                try
                {
                    var elms = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(cat)
                        .WhereElementIsNotElementType()
                        .ToList();
                    structuralElements.AddRange(elms);
                }
                catch { }
            }

            foreach (var tag in tags)
            {
                var tagBox = tag.get_BoundingBox(view);
                bool hasClash = false;

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

                // Check 3: Clash with other structural elements or other tags
                if (tagBox != null)
                {
                    // Clash with structural elements
                    foreach (var el in structuralElements)
                    {
                        if (el.Id.ToLongValue() == hostId.ToLongValue()) continue;

                        var elBox = el.get_BoundingBox(view);
                        if (elBox == null) continue;

                        if (tagBox.Min.X < elBox.Max.X && tagBox.Max.X > elBox.Min.X &&
                            tagBox.Min.Y < elBox.Max.Y && tagBox.Max.Y > elBox.Min.Y)
                        {
                            clashingTagIds.Add(tag.Id);
                            hasClash = true;
                            break;
                        }
                    }

                    // Clash with other tags (Tag-to-Tag clash)
                    if (!hasClash)
                    {
                        foreach (var otherTag in tags)
                        {
                            if (otherTag.Id.ToLongValue() == tag.Id.ToLongValue()) continue;

                            var otherBox = otherTag.get_BoundingBox(view);
                            if (otherBox == null) continue;

                            if (tagBox.Min.X < otherBox.Max.X && tagBox.Max.X > otherBox.Min.X &&
                                tagBox.Min.Y < otherBox.Max.Y && tagBox.Max.Y > otherBox.Min.Y)
                            {
                                clashingTagIds.Add(tag.Id);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public static void ApplyRedOverrideForClashes(Document doc, View view, List<ElementId> clashingTagIds)
        {
            using (var tx = new Transaction(doc, "K-TOOLS: Override Clashing Tags"))
            {
                tx.Start();
                var red = new Color(255, 0, 0);

                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .ToList();

                // Clear element-level overrides for all tags first
                foreach (var tag in tags)
                {
                    try
                    {
                        view.SetElementOverrides(tag.Id, new OverrideGraphicSettings());
                    }
                    catch { }
                }

                // Apply red override to clashing tags
                foreach (var tagId in clashingTagIds)
                {
                    try
                    {
                        var ogs = new OverrideGraphicSettings();
                        ogs.SetProjectionLineColor(red);
                        view.SetElementOverrides(tagId, ogs);
                    }
                    catch { }
                }

                tx.Commit();
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

        private static XYZ GetTagLeaderEnd(IndependentTag tag)
        {
            try
            {
                // Try GetLeaderEnd(Reference) - Revit 2023+
                var getLeaderEndMethod = tag.GetType().GetMethod("GetLeaderEnd", new Type[] { typeof(Reference) });
                if (getLeaderEndMethod != null)
                {
                    // Get the first tagged reference
                    var getRefsMethod = tag.GetType().GetMethod("GetTaggedReferences");
                    if (getRefsMethod != null)
                    {
                        var refs = getRefsMethod.Invoke(tag, null) as System.Collections.ICollection;
                        if (refs != null)
                        {
                            foreach (var r in refs)
                            {
                                var pt = getLeaderEndMethod.Invoke(tag, new object[] { r }) as XYZ;
                                if (pt != null)
                                {
                                    return pt;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                // Try LeaderEnd property - Revit 2022 and older
                var prop = tag.GetType().GetProperty("LeaderEnd");
                if (prop != null)
                {
                    return prop.GetValue(tag) as XYZ;
                }
            }
            catch { }

            return tag.TagHeadPosition;
        }

        public static bool ResetFloorTagHost(Document doc, View view, IndependentTag tag, out string successFloorName)
        {
            successFloorName = string.Empty;
            
            // Step 1: Determine correct checkpoint (Leader End or Tag Head)
            XYZ tagPoint = tag.TagHeadPosition;
            if (tag.HasLeader)
            {
                tagPoint = GetTagLeaderEnd(tag);
            }

            // Collect all floors visible in the view
            var visibleFloors = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .ToList();

            Floor targetFloor = null;

            // Stage 1: Strict Point-in-Polygon check with Z & Area sorting
            var candidates = new List<Tuple<Floor, double, double>>();

            foreach (var floor in visibleFloors)
            {
                // Filter out precast/others if not desired, but let's do a basic name check to bypass non-structural floors
                string floorName = floor.Name.ToLower();
                if (floorName.Contains("others") || floorName.Contains("precast") || floorName.Contains("existing"))
                {
                    continue;
                }

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
                                double area = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? double.MaxValue;
                                candidates.Add(Tuple.Create(floor, proj.XYZPoint.Z, area));
                            }
                        }
                    }
                }
                catch { }
            }

            if (candidates.Count > 0)
            {
                // Sort by Z descending (highest Z first), then by Area ascending (smallest area first)
                var sorted = candidates
                    .OrderByDescending(c => c.Item2)
                    .ThenBy(c => c.Item3)
                    .ToList();

                targetFloor = sorted[0].Item1;
            }

            // Stage 2: Boundary proximity fallback search (within 30cm / 1.0 foot tolerance)
            if (targetFloor == null)
            {
                var boundaryCandidates = new List<Tuple<Floor, double>>();

                foreach (var floor in visibleFloors)
                {
                    string floorName = floor.Name.ToLower();
                    if (floorName.Contains("others") || floorName.Contains("precast") || floorName.Contains("existing"))
                    {
                        continue;
                    }

                    try
                    {
                        var topFaceRefs = HostObjectUtils.GetTopFaces(floor);
                        double minDistForFloor = double.MaxValue;

                        foreach (var faceRef in topFaceRefs)
                        {
                            var face = floor.GetGeometryObjectFromReference(faceRef) as PlanarFace;
                            if (face != null)
                            {
                                foreach (var loop in face.GetEdgesAsCurveLoops())
                                {
                                    foreach (var curve in loop)
                                    {
                                        double dist = curve.Distance(tagPoint);
                                        if (dist < minDistForFloor)
                                        {
                                            minDistForFloor = dist;
                                        }
                                    }
                                }
                            }
                        }

                        // 1.0 internal foot is approximately 30.48 cm
                        if (minDistForFloor <= 1.0)
                        {
                            boundaryCandidates.Add(Tuple.Create(floor, minDistForFloor));
                        }
                    }
                    catch { }
                }

                if (boundaryCandidates.Count > 0)
                {
                    // Sort by distance ascending (closest first)
                    var sorted = boundaryCandidates
                        .OrderBy(bc => bc.Item2)
                        .ToList();

                    targetFloor = sorted[0].Item1;
                }
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

        public class TagProximityError
        {
            public ElementId TagId { get; set; }
            public ElementId HostId { get; set; }
            public string TagText { get; set; }
            public string IssueDescription { get; set; }
        }

        public static double Get2DDistanceToElement(Element el, XYZ checkPoint, View view)
        {
            double minDist = double.MaxValue;
            var opt = new Options { DetailLevel = ViewDetailLevel.Medium, View = view };
            var geom = el.get_Geometry(opt);
            if (geom == null) return double.MaxValue;

            XYZ pt2D = new XYZ(checkPoint.X, checkPoint.Y, 0);

            var solids = new List<Solid>();
            GetSolidsFromGeometry(geom, solids);

            foreach (var solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    try
                    {
                        var proj = face.Project(checkPoint);
                        if (proj != null)
                        {
                            XYZ projPt2D = new XYZ(proj.XYZPoint.X, proj.XYZPoint.Y, 0);
                            double d = pt2D.DistanceTo(projPt2D);
                            if (d < minDist) minDist = d;
                        }

                        foreach (var loop in face.GetEdgesAsCurveLoops())
                        {
                            foreach (Curve curve in loop)
                            {
                                var curveProj = curve.Project(checkPoint);
                                if (curveProj != null)
                                {
                                    XYZ curvePt2D = new XYZ(curveProj.XYZPoint.X, curveProj.XYZPoint.Y, 0);
                                    double d = pt2D.DistanceTo(curvePt2D);
                                    if (d < minDist) minDist = d;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            return minDist;
        }

        private static void GetSolidsFromGeometry(GeometryElement geom, List<Solid> solids)
        {
            if (geom == null) return;
            foreach (var obj in geom)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    solids.Add(solid);
                }
                else if (obj is GeometryInstance inst)
                {
                    var instGeom = inst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        GetSolidsFromGeometry(instGeom, solids);
                    }
                    else
                    {
                        var symGeom = inst.GetSymbolGeometry();
                        GetSolidsFromGeometry(symGeom, solids);
                    }
                }
            }
        }

        public static void AuditTagsProximity(
            Document doc,
            View view,
            double maxErrorDistanceMm,
            out List<TagProximityError> columnErrors,
            out List<TagProximityError> wallErrors,
            out List<TagProximityError> floorErrors)
        {
            columnErrors = new List<TagProximityError>();
            wallErrors = new List<TagProximityError>();
            floorErrors = new List<TagProximityError>();

            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            foreach (var tag in tags)
            {
                ElementId hostId = GetTaggedElementId(tag);
                if (hostId == null || hostId == ElementId.InvalidElementId) continue;

                Element host = doc.GetElement(hostId);
                if (host == null) continue;

                XYZ checkPoint = tag.TagHeadPosition;
                if (tag.HasLeader)
                {
                    checkPoint = GetTagLeaderEnd(tag);
                }

                double distFeet = Get2DDistanceToElement(host, checkPoint, view);
                if (distFeet == double.MaxValue) continue;

                double distMm = distFeet * 304.8;

                if (distMm > maxErrorDistanceMm)
                {
                    string tagText = string.Empty;
                    try
                    {
                        tagText = tag.TagText;
                    }
                    catch
                    {
                        tagText = tag.Name;
                    }

                    var err = new TagProximityError
                    {
                        TagId = tag.Id,
                        HostId = host.Id,
                        TagText = tagText ?? string.Empty,
                        IssueDescription = $"Distance Mismatch (Tag is > {maxErrorDistanceMm:0}mm away from Boundary)"
                    };

                    var cat = host.Category;
                    if (cat != null)
                    {
                        long catVal = cat.Id.ToLongValue();
                        if (catVal == (long)BuiltInCategory.OST_StructuralColumns)
                        {
                            columnErrors.Add(err);
                        }
                        else if (catVal == (long)BuiltInCategory.OST_Walls)
                        {
                            wallErrors.Add(err);
                        }
                        else if (catVal == (long)BuiltInCategory.OST_Floors)
                        {
                            floorErrors.Add(err);
                        }
                    }
                }
            }
        }

        public static void ApplyRedOverrideForHostAndTags(Document doc, View view, List<ElementId> elementIds)
        {
            using (var tx = new Transaction(doc, "K-TOOLS: Highlight Proximity Errors"))
            {
                tx.Start();
                var red = new Color(255, 0, 0);

                foreach (var id in elementIds)
                {
                    try
                    {
                        var ogs = new OverrideGraphicSettings();
                        ogs.SetProjectionLineColor(red);
                        view.SetElementOverrides(id, ogs);
                    }
                    catch { }
                }

                tx.Commit();
            }
        }

        public static void ResetElementOverrides(Document doc, View view, List<ElementId> elementIds)
        {
            using (var tx = new Transaction(doc, "K-TOOLS: Reset Graphic Overrides"))
            {
                tx.Start();

                foreach (var id in elementIds)
                {
                    try
                    {
                        view.SetElementOverrides(id, new OverrideGraphicSettings());
                    }
                    catch { }
                }

                tx.Commit();
            }
        }
    }
}
