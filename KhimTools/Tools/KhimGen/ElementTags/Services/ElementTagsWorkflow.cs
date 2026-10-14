using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Revit;
using KhimTools.Core;
using KhimTools.ElementTags.Models;

namespace KhimTools.ElementTags.Services
{
    /// <summary>Stable audit vocabulary used by the Elements Tags workflow.</summary>
    public enum TagAuditStatus
    {
        READY,
        MISSING,
        DUPLICATE,
        ORPHAN,
        WRONG_TYPE,
        HIDDEN_HOST,
        MISPLACED,
        CLASH,
        PINNED,
        READ_ONLY,
        NO_SYMBOL,
        NO_ANCHOR,
        NO_CHANGE,
        UNSUPPORTED_VIEW,
        FAILED,
        SKIP
    }

    public enum TagActionType
    {
        CREATE,
        CHANGE_TYPE,
        MOVE_ALIGN,
        LEADER_UPDATE,
        SKIP
    }

    public sealed class TagHostRecord
    {
        public ElementId HostId { get; set; }
        public string UniqueId { get; set; }
        public BuiltInCategory HostCategory { get; set; }
        public ElementId ViewId { get; set; }
        public XYZ Anchor { get; set; }
        public bool IsVisible { get; set; }
        public bool IsSupported { get; set; }
    }

    public sealed class TagRelationshipRecord
    {
        public ElementId TagId { get; set; }
        public ElementId HostId { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId TagTypeId { get; set; }
        public BuiltInCategory TagCategory { get; set; }
        public XYZ HeadPosition { get; set; }
        public bool HasLeader { get; set; }
        public bool Pinned { get; set; }
        public bool IsOrphan { get; set; }
        public bool HostIsVisible { get; set; }
        public string UniqueId { get; set; }
    }

    /// <summary>
    /// Deterministic host-to-tag index. It deliberately keeps orphan tags so the
    /// audit can report them instead of silently dropping damaged relationships.
    /// </summary>
    public sealed class TagRelationshipIndex
    {
        public Document Document { get; private set; }
        public View View { get; private set; }
        public List<TagHostRecord> Hosts { get; private set; } = new List<TagHostRecord>();
        public List<TagRelationshipRecord> Tags { get; private set; } = new List<TagRelationshipRecord>();
        public Dictionary<ElementId, List<TagRelationshipRecord>> TagsByHost { get; private set; }
            = new Dictionary<ElementId, List<TagRelationshipRecord>>();
        public Dictionary<ElementId, TagRelationshipRecord> TagsById { get; private set; }
            = new Dictionary<ElementId, TagRelationshipRecord>();

        private TagRelationshipIndex() { }

        public static TagRelationshipIndex Build(Document doc, View view)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (view == null) throw new ArgumentNullException("view");
            var index = new TagRelationshipIndex { Document = doc, View = view };
            var visible = new HashSet<long>();
            try
            {
                foreach (ElementId id in new FilteredElementCollector(doc, view.Id).ToElementIds())
                    visible.Add(IdValue(id));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][ElementTags] visible host collection failed: " + ex);
            }

            foreach (KeyValuePair<BuiltInCategory, BuiltInCategory> mapping in ElementTagsService.HostToTagCategoryMap
                .OrderBy(pair => (int)pair.Key))
            {
                try
                {
                    List<Element> hosts = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(mapping.Key).WhereElementIsNotElementType().ToList();
                    foreach (Element host in hosts.OrderBy(item => item.UniqueId, StringComparer.Ordinal))
                    {
                        XYZ anchor = null;
                        try { anchor = TagPlacement.Anchor(host, view); }
                        catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] anchor failed: " + ex); }
                        index.Hosts.Add(new TagHostRecord
                        {
                            HostId = host.Id,
                            UniqueId = host.UniqueId ?? string.Empty,
                            HostCategory = mapping.Key,
                            ViewId = view.Id,
                            Anchor = anchor,
                            IsVisible = visible.Contains(IdValue(host.Id)),
                            IsSupported = anchor != null
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[K-TOOLS][ElementTags] host collection failed for " + mapping.Key + ": " + ex);
                }
            }
            index.Hosts = index.Hosts
                .OrderBy(item => (int)item.HostCategory)
                .ThenBy(item => item.UniqueId, StringComparer.Ordinal)
                .ToList();

            try
            {
                List<IndependentTag> tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag)).Cast<IndependentTag>()
                    .OrderBy(item => item.UniqueId, StringComparer.Ordinal).ToList();
                foreach (IndependentTag tag in tags)
                {
                    ElementId hostId = GetTaggedLocalElementId(tag);
                    bool orphan = hostId == null || hostId == ElementId.InvalidElementId || doc.GetElement(hostId) == null;
                    ElementId typeId = null;
                    try { typeId = tag.GetTypeId(); } catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] tag type read failed: " + ex); }
                    BuiltInCategory tagCategory = BuiltInCategory.INVALID;
                    try
                    {
                        Category category = tag.Category;
                        if (category != null) tagCategory = (BuiltInCategory)category.Id.ToLongValue();
                    }
                    catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] tag category read failed: " + ex); }
                    XYZ head = null;
                    try { head = tag.TagHeadPosition; } catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] tag head read failed: " + ex); }
                    bool leader = false;
                    try { leader = tag.HasLeader; } catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] tag leader read failed: " + ex); }
                    var record = new TagRelationshipRecord
                    {
                        TagId = tag.Id,
                        HostId = hostId,
                        ViewId = view.Id,
                        TagTypeId = typeId,
                        TagCategory = tagCategory,
                        HeadPosition = head,
                        HasLeader = leader,
                        Pinned = tag.Pinned,
                        IsOrphan = orphan,
                        HostIsVisible = !orphan && visible.Contains(IdValue(hostId)),
                        UniqueId = tag.UniqueId ?? string.Empty
                    };
                    index.Tags.Add(record);
                    index.TagsById[record.TagId] = record;
                    if (!orphan)
                    {
                        List<TagRelationshipRecord> related;
                        if (!index.TagsByHost.TryGetValue(hostId, out related))
                        {
                            related = new List<TagRelationshipRecord>();
                            index.TagsByHost[hostId] = related;
                        }
                        related.Add(record);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][ElementTags] tag collection failed: " + ex);
            }
            return index;
        }

        public static long IdValue(ElementId id)
        {
            return id == null || id == ElementId.InvalidElementId ? long.MinValue : id.ToLongValue();
        }

        private static ElementId GetTaggedLocalElementId(IndependentTag tag)
        {
            try
            {
                MethodInfoShim method = MethodInfoShim.Find(tag.GetType(), "GetTaggedLocalElementIds");
                if (method != null)
                {
                    IEnumerable values = method.Invoke(tag, null) as IEnumerable;
                    if (values != null)
                    {
                        foreach (object value in values)
                        {
                            ElementId id = value as ElementId;
                            if (id != null) return id;
                            var hostProperty = value.GetType().GetProperty("HostElementId");
                            if (hostProperty != null) return hostProperty.GetValue(value, null) as ElementId;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] local tag relationship read failed: " + ex); }
            try
            {
                var property = tag.GetType().GetProperty("TaggedLocalElementId");
                if (property != null) return property.GetValue(tag, null) as ElementId;
            }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] legacy tag relationship read failed: " + ex); }
            return ElementId.InvalidElementId;
        }

        private sealed class MethodInfoShim
        {
            private readonly System.Reflection.MethodInfo _method;
            private MethodInfoShim(System.Reflection.MethodInfo method) { _method = method; }
            public object Invoke(object instance, object[] args) { return _method.Invoke(instance, args); }
            public static MethodInfoShim Find(Type type, string name)
            {
                System.Reflection.MethodInfo method = type.GetMethod(name);
                return method == null ? null : new MethodInfoShim(method);
            }
        }
    }

    public sealed class TagAuditItem
    {
        public ElementId HostId { get; set; }
        public ElementId TagId { get; set; }
        public TagAuditStatus Status { get; set; }
        public string Message { get; set; }
        public bool CanExecute { get; set; }
        public bool IsHostIssue { get; set; }
        public bool Pinned { get; set; }
        public ElementId CurrentTypeId { get; set; }
        public ElementId ExpectedTypeId { get; set; }
        public XYZ Anchor { get; set; }
        public double Distance { get; set; }
    }

    public sealed class TagAuditResult
    {
        public TagRelationshipIndex Index { get; set; }
        public List<TagAuditItem> Items { get; private set; } = new List<TagAuditItem>();
        public int Ready { get { return Items.Count(item => item.Status == TagAuditStatus.READY); } }
        public int Missing { get { return Items.Count(item => item.Status == TagAuditStatus.MISSING); } }
        public int Duplicate { get { return Items.Count(item => item.Status == TagAuditStatus.DUPLICATE); } }
        public int Orphan { get { return Items.Count(item => item.Status == TagAuditStatus.ORPHAN); } }
        public int Actionable { get { return Items.Count(item => item.CanExecute); } }
    }

    public sealed class ElementTagWorkflowOptions
    {
        public bool AddLeader { get; set; }
        public bool OnlyUntagged { get; set; }
        public bool AlignExisting { get; set; } = true;
        public bool UpdateLeader { get; set; }
        public List<ElementId> SelectedHostIds { get; set; } = new List<ElementId>();
        public TagHeightRange HeightRange { get; set; }
        public List<ElementTagsItem> Configurations { get; set; } = new List<ElementTagsItem>();
    }

    public sealed class TagActionPlanItem
    {
        public TagActionType Action { get; set; }
        public TagAuditStatus SourceStatus { get; set; }
        public ElementId HostId { get; set; }
        public ElementId TagId { get; set; }
        public ElementId TargetTypeId { get; set; }
        public XYZ TargetAnchor { get; set; }
        public bool AddLeader { get; set; }
        public string Reason { get; set; }
        public bool CanExecute { get; set; }
    }

    public sealed class TagActionPlan
    {
        public List<TagActionPlanItem> Items { get; private set; } = new List<TagActionPlanItem>();
        public int Ready { get { return Items.Count(item => item != null && item.CanExecute); } }
        public int Skipped { get { return Items.Count(item => item == null || !item.CanExecute); } }
    }

    public sealed class TagPreflightReport
    {
        public TagRelationshipIndex Index { get; set; }
        public TagAuditResult Audit { get; set; }
        public List<ElementTagsItem> Configurations { get; set; } = new List<ElementTagsItem>();
        public TagActionPlan Plan { get; set; } = new TagActionPlan();
        public List<TagActionPlanItem> ActionPlan { get; private set; } = new List<TagActionPlanItem>();
        public List<TagAuditItem> Skipped { get; private set; } = new List<TagAuditItem>();
        public List<string> Errors { get; private set; } = new List<string>();
        public bool IsValid { get { return Errors.Count == 0; } }
        public int Ready { get { return ActionPlan.Count(item => item.CanExecute); } }
    }

    public sealed class TagExecutionResult
    {
        public TagActionType Action { get; set; }
        public ElementId HostId { get; set; }
        public ElementId TagId { get; set; }
        public TagAuditStatus Status { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; }
    }

    public sealed class TagBatchResult
    {
        public int Selected { get; set; }
        public int Created { get; set; }
        public int ChangedType { get; set; }
        public int Moved { get; set; }
        public int LeadersUpdated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public TagPreflightReport Preflight { get; set; }
        public TagAuditResult Verification { get; set; }
        public List<TagExecutionResult> Results { get; private set; } = new List<TagExecutionResult>();
    }

    public static class TagAuditService
    {
        public static TagAuditResult Audit(Document doc, View view, TagRelationshipIndex index,
            IEnumerable<ElementTagsItem> configurations)
        {
            var result = new TagAuditResult { Index = index };
            var expected = (configurations ?? Enumerable.Empty<ElementTagsItem>())
                .Where(item => item != null && item.SelectedTagSymbol != null)
                .ToDictionary(item => item.HostCategory, item => item.SelectedTagSymbol.Id);
            var hosts = index.Hosts.ToDictionary(item => item.HostId, item => item);
            foreach (TagRelationshipRecord orphan in index.Tags.Where(item => item.IsOrphan))
            {
                result.Items.Add(new TagAuditItem
                {
                    TagId = orphan.TagId, HostId = orphan.HostId, Status = TagAuditStatus.ORPHAN,
                    Pinned = orphan.Pinned, Message = "Tag không còn host cục bộ trong view.", IsHostIssue = false
                });
            }
            foreach (TagRelationshipRecord hidden in index.Tags.Where(item => !item.IsOrphan && !hosts.ContainsKey(item.HostId)))
            {
                result.Items.Add(new TagAuditItem
                {
                    TagId = hidden.TagId, HostId = hidden.HostId, Status = TagAuditStatus.HIDDEN_HOST,
                    Pinned = hidden.Pinned, Message = "Host tồn tại nhưng không hiển thị trong view.", IsHostIssue = false
                });
            }
            foreach (TagHostRecord host in index.Hosts)
            {
                List<TagRelationshipRecord> related;
                if (!index.TagsByHost.TryGetValue(host.HostId, out related) || related.Count == 0)
                {
                    result.Items.Add(new TagAuditItem { HostId = host.HostId, Status = TagAuditStatus.MISSING,
                        CanExecute = expected.ContainsKey(host.HostCategory) && host.Anchor != null,
                        Anchor = host.Anchor, IsHostIssue = true, Message = "Host chưa có tag." });
                    continue;
                }
                bool duplicate = related.Count > 1;
                foreach (TagRelationshipRecord tag in related.OrderBy(item => item.UniqueId, StringComparer.Ordinal))
                {
                    TagAuditStatus status = TagAuditStatus.READY;
                    string message = "Tag hợp lệ.";
                    bool canExecute = true;
                    ElementId expectedType;
                    if (duplicate) { status = TagAuditStatus.DUPLICATE; message = "Host có nhiều tag trong view."; canExecute = false; }
                    else if (!host.IsVisible) { status = TagAuditStatus.HIDDEN_HOST; message = "Host không hiển thị trong view."; canExecute = false; }
                    else if (tag.Pinned) { status = TagAuditStatus.PINNED; message = "Tag bị pin nên không tự chỉnh."; canExecute = false; }
                    else if (!expected.TryGetValue(host.HostCategory, out expectedType))
                    { status = TagAuditStatus.NO_SYMBOL; message = "Chưa có tag type đã chọn."; canExecute = false; }
                    else if (tag.TagTypeId == null || TagRelationshipIndex.IdValue(tag.TagTypeId) != TagRelationshipIndex.IdValue(expectedType))
                    { status = TagAuditStatus.WRONG_TYPE; message = "Tag type khác type đã chọn."; }
                    else if (IsMisplaced(doc, view, host, tag))
                    { status = TagAuditStatus.MISPLACED; message = "Tag lệch khỏi anchor đã căn chỉnh."; }
                    else if (IsClash(doc, view, index, host, tag))
                    { status = TagAuditStatus.CLASH; message = "Tag đang va chạm với tag hoặc hình học khác."; }
                    result.Items.Add(new TagAuditItem
                    {
                        HostId = host.HostId, TagId = tag.TagId, Status = status, Message = message,
                        CanExecute = canExecute, Pinned = tag.Pinned, Anchor = host.Anchor,
                        CurrentTypeId = tag.TagTypeId, ExpectedTypeId = expected.ContainsKey(host.HostCategory) ? expected[host.HostCategory] : null,
                        IsHostIssue = false
                    });
                }
            }
            return result;
        }

        private static bool IsMisplaced(Document doc, View view, TagHostRecord host, TagRelationshipRecord tag)
        {
            if (host.Anchor == null || tag.HeadPosition == null) return true;
            double threshold = TagPlacement.PaperDistance(view, 12);
            return tag.HeadPosition.DistanceTo(host.Anchor) > threshold;
        }

        private static bool IsClash(Document doc, View view, TagRelationshipIndex index, TagHostRecord host, TagRelationshipRecord tag)
        {
            IndependentTag current = doc.GetElement(tag.TagId) as IndependentTag;
            if (current == null) return false;
            double[] currentBounds = ProjectedBounds(current, view);
            if (currentBounds == null) return false;
            foreach (TagRelationshipRecord other in index.Tags.Where(item => item.TagId != tag.TagId && !item.IsOrphan))
            {
                IndependentTag otherTag = doc.GetElement(other.TagId) as IndependentTag;
                double[] otherBounds = otherTag == null ? null : ProjectedBounds(otherTag, view);
                if (otherBounds != null && Overlap(currentBounds, otherBounds)) return true;
            }
            foreach (TagHostRecord otherHost in index.Hosts.Where(item => item.HostId != host.HostId))
            {
                Element structural = doc.GetElement(otherHost.HostId);
                double[] hostBounds = structural == null ? null : ProjectedBounds(structural, view);
                if (hostBounds != null && Overlap(currentBounds, hostBounds)) return true;
            }
            return false;
        }

        private static double[] ProjectedBounds(Element element, View view)
        {
            try
            {
                BoundingBoxXYZ box = element.get_BoundingBox(view);
                if (box == null) return null;
                Transform transform = box.Transform ?? Transform.Identity;
                double left = double.PositiveInfinity, bottom = double.PositiveInfinity;
                double right = double.NegativeInfinity, top = double.NegativeInfinity;
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                {
                    XYZ local = new XYZ(x == 0 ? box.Min.X : box.Max.X, y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z);
                    XYZ point = transform.OfPoint(local);
                    double u = point.DotProduct(view.RightDirection), v = point.DotProduct(view.UpDirection);
                    left = Math.Min(left, u); right = Math.Max(right, u); bottom = Math.Min(bottom, v); top = Math.Max(top, v);
                }
                return new[] { left, bottom, right, top };
            }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] projected bounds failed: " + ex); return null; }
        }

        private static bool Overlap(double[] a, double[] b)
        {
            return a[0] < b[2] && a[2] > b[0] && a[1] < b[3] && a[3] > b[1];
        }
    }

    public static class TagPreflightService
    {
        public static TagPreflightReport Preflight(Document doc, View view, ElementTagWorkflowOptions options)
        {
            var report = new TagPreflightReport();
            if (doc == null || view == null) { report.Errors.Add("Document hoặc view không tồn tại."); return report; }
            if (doc.IsReadOnly) report.Errors.Add("Document đang read-only.");
            if (view is ViewSheet) report.Errors.Add("Không thể gắn tag trực tiếp trên Sheet.");
            var view3D = view as View3D;
            if (view3D != null && !view3D.IsLocked) report.Errors.Add("View 3D phải được khóa trước khi gắn tag.");
            report.Index = TagRelationshipIndex.Build(doc, view);
            report.Configurations = options == null || options.Configurations == null
                ? new List<ElementTagsItem>() : options.Configurations.Where(item => item != null).ToList();
            report.Audit = TagAuditService.Audit(doc, view, report.Index, options == null ? null : options.Configurations);
            if (options == null) options = new ElementTagWorkflowOptions();
            if (options.Configurations == null) options.Configurations = new List<ElementTagsItem>();
            HashSet<long> selected = new HashSet<long>((options.SelectedHostIds ?? new List<ElementId>()).Select(TagRelationshipIndex.IdValue));
            bool hasSelection = selected.Count > 0;
            foreach (TagAuditItem item in report.Audit.Items.OrderBy(item => TagRelationshipIndex.IdValue(item.HostId)).ThenBy(item => TagRelationshipIndex.IdValue(item.TagId)))
            {
                TagActionPlanItem plan = ToPlan(doc, view, report.Index, item, options, selected, hasSelection);
                if (plan != null && plan.CanExecute) { report.ActionPlan.Add(plan); report.Plan.Items.Add(plan); }
                else report.Skipped.Add(item);
            }
            return report;
        }

        public static TagPreflightReport Run(Document doc, View view, ElementTagWorkflowOptions options)
        {
            return Preflight(doc, view, options);
        }

        private static TagActionPlanItem ToPlan(Document doc, View view, TagRelationshipIndex index, TagAuditItem item,
            ElementTagWorkflowOptions options, HashSet<long> selected, bool hasSelection)
        {
            if (item == null || item.Status == TagAuditStatus.ORPHAN || item.Status == TagAuditStatus.DUPLICATE || item.Status == TagAuditStatus.HIDDEN_HOST || item.Status == TagAuditStatus.NO_SYMBOL)
                return null;
            if (item.HostId == null || (hasSelection && !selected.Contains(TagRelationshipIndex.IdValue(item.HostId)))) return null;
            TagHostRecord host = index.Hosts.FirstOrDefault(candidate => candidate.HostId == item.HostId);
            if (host == null || host.Anchor == null) return null;
            Element hostElement = doc.GetElement(item.HostId);
            if (options.HeightRange != null && (hostElement == null || !options.HeightRange.Contains(hostElement, view))) return null;
            if (item.Status == TagAuditStatus.MISSING)
            {
                ElementTagsItem config = options.Configurations.FirstOrDefault(candidate => candidate.HostCategory == host.HostCategory && candidate.SelectedTagSymbol != null);
                if (config == null || (options.OnlyUntagged == false && item.Status != TagAuditStatus.MISSING)) return null;
                return new TagActionPlanItem { Action = TagActionType.CREATE, SourceStatus = item.Status, HostId = item.HostId,
                    TargetTypeId = config.SelectedTagSymbol.Id, TargetAnchor = host.Anchor, AddLeader = options.AddLeader,
                    Reason = "Tạo tag còn thiếu", CanExecute = true };
            }
            if (!item.CanExecute || item.Pinned) return null;
            if (item.Status == TagAuditStatus.WRONG_TYPE)
                return new TagActionPlanItem { Action = TagActionType.CHANGE_TYPE, SourceStatus = item.Status, HostId = item.HostId,
                    TagId = item.TagId, TargetTypeId = item.ExpectedTypeId, TargetAnchor = host.Anchor,
                    Reason = "Đổi về tag type đã chọn", CanExecute = item.ExpectedTypeId != null };
            if (item.Status == TagAuditStatus.MISPLACED || item.Status == TagAuditStatus.CLASH)
                return new TagActionPlanItem { Action = TagActionType.MOVE_ALIGN, SourceStatus = item.Status, HostId = item.HostId,
                    TagId = item.TagId, TargetAnchor = host.Anchor, Reason = "Căn chỉnh theo anchor và tránh va chạm",
                    CanExecute = options.AlignExisting };
            if (options.UpdateLeader && item.Status == TagAuditStatus.READY)
                return new TagActionPlanItem { Action = TagActionType.LEADER_UPDATE, SourceStatus = item.Status, HostId = item.HostId,
                    TagId = item.TagId, AddLeader = options.AddLeader, Reason = "Cập nhật leader theo cấu hình", CanExecute = true };
            return null;
        }
    }

    public static class TagActionExecutor
    {
        public static TagBatchResult Execute(Document doc, View view, TagPreflightReport preflight)
        {
            var result = new TagBatchResult { Preflight = preflight };
            if (preflight == null || !preflight.IsValid) return result;
            result.Selected = preflight.Audit == null ? 0 : preflight.Audit.Items.Count;
            result.Skipped = preflight.Skipped.Count;
            List<IndependentTag> existing = new FilteredElementCollector(doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>().ToList();
            List<Element> hostElements = preflight.Index.Hosts
                .Select(item => doc.GetElement(item.HostId)).Where(item => item != null).ToList();
            Dictionary<ElementId, XYZ> alignedAnchors = TagPlacement.AlignedAnchors(hostElements, view);
            try
            {
                using (var group = new TransactionGroup(doc, "K-TOOLS: Elements Tags 2.0"))
                {
                    TransactionBoundary.Start(group, "ElementTags.Batch");
                    try
                    {
                        using (var transaction = new Transaction(doc, "K-TOOLS: Elements Tags actions"))
                        {
                            TransactionBoundary.Start(transaction, "ElementTags.Actions");
                            List<double[]> occupied = TagPlacement.Obstacles(doc, view, existing);
                            foreach (TagActionPlanItem plan in preflight.ActionPlan.Where(item => item.CanExecute)
                                .OrderBy(item => TagRelationshipIndex.IdValue(item.HostId)).ThenBy(item => TagRelationshipIndex.IdValue(item.TagId)))
                            {
                                XYZ aligned;
                                if (alignedAnchors.TryGetValue(plan.HostId, out aligned)) plan.TargetAnchor = aligned;
                                ExecuteOne(doc, view, preflight.Index, plan, occupied, result);
                            }
                            TransactionBoundary.Commit(transaction, "ElementTags.Actions");
                        }
                        TransactionBoundary.Assimilate(group, "ElementTags.Batch");
                    }
                    catch
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            TransactionBoundary.RollBack(group, "ElementTags.Batch");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][ElementTags] action group failed: " + ex);
                List<TagExecutionResult> rolledBack = result.Results.Where(item => item.Changed).ToList();
                foreach (TagExecutionResult item in rolledBack)
                {
                    item.Changed = false;
                    item.Status = TagAuditStatus.FAILED;
                    item.Message = "Action group rolled back: " + ex.Message;
                }
                result.Created = 0;
                result.ChangedType = 0;
                result.Moved = 0;
                result.LeadersUpdated = 0;
                result.Failed += rolledBack.Count;
                result.Failed++;
            }
            result.Verification = TagAuditService.Audit(doc, view, TagRelationshipIndex.Build(doc, view), preflight.Configurations);
            return result;
        }

        private static void ExecuteOne(Document doc, View view, TagRelationshipIndex index, TagActionPlanItem plan,
            List<double[]> occupied, TagBatchResult result)
        {
            using (var sub = new SubTransaction(doc))
            {
                TransactionBoundary.Start(sub, "ElementTags." + plan.Action);
                try
                {
                    bool changed = false;
                    ElementId resultTagId = plan.TagId;
                    string successMessage = "Action applied.";
                    if (plan.Action == TagActionType.CREATE)
                    {
                        FamilySymbol symbol = doc.GetElement(plan.TargetTypeId) as FamilySymbol;
                        if (symbol == null) throw new InvalidOperationException("Không tìm thấy tag type.");
                        if (!symbol.IsActive) symbol.Activate();
                        Element host = doc.GetElement(plan.HostId);
                        if (host == null) throw new InvalidOperationException("Không tìm thấy host.");
                        IndependentTag tag = IndependentTag.Create(doc, symbol.Id, view.Id, new Reference(host),
                            plan.AddLeader, TagOrientation.Horizontal, plan.TargetAnchor);
                        if (tag == null || !TagPlacement.Place(doc, view, tag, plan.TargetAnchor, occupied, host, true))
                            throw new InvalidOperationException("Không tìm được vị trí tag trong giới hạn 12 mm trên giấy.");
                        resultTagId = tag.Id;
                        successMessage = "Tag created.";
                        changed = true;
                    }
                    else
                    {
                        IndependentTag tag = doc.GetElement(plan.TagId) as IndependentTag;
                        if (tag == null) throw new InvalidOperationException("Không tìm thấy tag.");
                        if (plan.Action == TagActionType.CHANGE_TYPE)
                        {
                            tag.ChangeTypeId(plan.TargetTypeId); changed = true;
                        }
                        else if (plan.Action == TagActionType.MOVE_ALIGN)
                        {
                            if (!TagPlacement.Place(doc, view, tag, plan.TargetAnchor, occupied, doc.GetElement(plan.HostId), false))
                                throw new InvalidOperationException("Không tìm được vị trí tránh clash trong giới hạn 12 mm trên giấy.");
                            changed = true;
                        }
                        else if (plan.Action == TagActionType.LEADER_UPDATE)
                        {
                            tag.HasLeader = plan.AddLeader; changed = true;
                        }
                    }
                    TransactionBoundary.Commit(sub, "ElementTags." + plan.Action);
                    if (changed)
                    {
                        if (plan.Action == TagActionType.CREATE) result.Created++;
                        else if (plan.Action == TagActionType.CHANGE_TYPE) result.ChangedType++;
                        else if (plan.Action == TagActionType.MOVE_ALIGN) result.Moved++;
                        else if (plan.Action == TagActionType.LEADER_UPDATE) result.LeadersUpdated++;
                    }
                    result.Results.Add(new TagExecutionResult { Action = plan.Action, HostId = plan.HostId, TagId = resultTagId,
                        Status = TagAuditStatus.READY, Changed = changed, Message = successMessage });
                }
                catch (Exception ex)
                {
                    TransactionBoundary.RollBack(sub, "ElementTags." + plan.Action);
                    result.Failed++;
                    result.Results.Add(new TagExecutionResult { Action = plan.Action, HostId = plan.HostId, TagId = plan.TagId,
                        Status = TagAuditStatus.FAILED, Changed = false, Message = ex.Message });
                    Debug.WriteLine("[K-TOOLS][ElementTags] action failed: " + ex);
                }
            }
        }
    }
}
