using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;

namespace KhimTools.TextAlign.Services
{
    public enum AlignType
    {
        Top,
        Bottom,
        Left,
        Right,
        Middle,
        HorizontalEquals,
        VerticalEquals
    }

    public enum TextAlignElementKind
    {
        TextNote,
        IndependentTag,
        GenericAnnotation,
        DetailItem,
        Unsupported
    }

    public enum TextAlignStatusCode
    {
        READY,
        NO_CHANGE,
        UNSUPPORTED_ELEMENT,
        PINNED,
        NOT_MOVABLE,
        CONSTRAINED,
        MISSING_ELEMENT,
        NO_BOUNDING_BOX,
        NOT_ENOUGH_TARGETS,
        SOURCE_REFERENCE,
        FAILED,
        UNSUPPORTED_VIEW
    }

    public enum TextAlignReferenceStrategy
    {
        AUTO_EXTREME,
        EXPLICIT_REFERENCE
    }

    public struct ProjectedBounds
    {
        public double Left;
        public double Right;
        public double Top;
        public double Bottom;
        public double CenterU;
        public double CenterV;

        public double Width { get { return Math.Max(0, Right - Left); } }
        public double Height { get { return Math.Max(0, Top - Bottom); } }

        public bool IsValid
        {
            get { return !double.IsNaN(Left) && Right >= Left && Top >= Bottom; }
        }

        public ProjectedBounds(double left, double right, double top, double bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            CenterU = (left + right) * 0.5;
            CenterV = (bottom + top) * 0.5;
        }
    }

    public sealed class TextAlignItem
    {
        public ElementId ElementId { get; set; }
        public TextAlignElementKind ElementKind { get; set; }
        public ElementId ViewId { get; set; }
        public ProjectedBounds Bounds { get; set; }
        public bool Pinned { get; set; }
        public bool Supported { get; set; }
    }

    public sealed class TextAlignPreflightResult
    {
        public ElementId ElementId { get; set; }
        public TextAlignElementKind ElementKind { get; set; }
        public AlignType Operation { get; set; }
        public int Severity { get; set; }
        public TextAlignStatusCode Status { get; set; }
        public string Message { get; set; }
        public bool CanExecute { get; set; }
        public TextAlignItem Item { get; set; }
    }

    public sealed class TextAlignExecutionResult
    {
        public ElementId ElementId { get; set; }
        public TextAlignElementKind ElementKind { get; set; }
        public AlignType Operation { get; set; }
        public XYZ OldCenter { get; set; }
        public XYZ TargetCenter { get; set; }
        public bool Changed { get; set; }
        public TextAlignStatusCode Status { get; set; }
        public string Message { get; set; }
    }

    public sealed class TextAlignBatchResult
    {
        public int Selected { get; set; }
        public int Ready { get; set; }
        public int Changed { get; set; }
        public int AlreadyAligned { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool Cancelled { get; set; }
        public string Message { get; set; }
        public List<TextAlignPreflightResult> Preflight { get; } = new List<TextAlignPreflightResult>();
        public List<TextAlignExecutionResult> Results { get; } = new List<TextAlignExecutionResult>();
    }

    public static class ViewPlaneGeometry
    {
        public const double Tolerance = 0.0001;

        public static bool TryGetBasis(View view, out XYZ right, out XYZ up, out XYZ normal, out string error)
        {
            right = null;
            up = null;
            normal = null;
            error = string.Empty;
            if (view == null)
            {
                error = "Active view is missing.";
                return false;
            }
            if (view.ViewType == ViewType.ThreeD || view.ViewType == ViewType.Internal ||
                view.ViewType == ViewType.ProjectBrowser || view.ViewType == ViewType.SystemBrowser)
            {
                error = "The active view does not provide a supported 2D annotation plane.";
                return false;
            }
            try
            {
                right = view.RightDirection.Normalize();
                up = view.UpDirection.Normalize();
                normal = view.ViewDirection.Normalize();
                if (right.IsZeroLength() || up.IsZeroLength() || normal.IsZeroLength())
                {
                    error = "The active view basis is invalid.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.WriteLine("[K-TOOLS][TextAlign] view basis failed: " + ex);
                return false;
            }
        }

        public static bool TryProjectBoundingBox(Element element, View view, out ProjectedBounds bounds, out string error)
        {
            bounds = new ProjectedBounds(double.NaN, double.NaN, double.NaN, double.NaN);
            error = string.Empty;
            XYZ right;
            XYZ up;
            XYZ normal;
            if (!TryGetBasis(view, out right, out up, out normal, out error)) return false;
            try
            {
                BoundingBoxXYZ box = element == null ? null : element.get_BoundingBox(view);
                if (box == null)
                {
                    error = "Element has no bounding box in the active view.";
                    return false;
                }
                Transform transform = box.Transform ?? Transform.Identity;
                double left = double.PositiveInfinity;
                double rightValue = double.NegativeInfinity;
                double bottom = double.PositiveInfinity;
                double top = double.NegativeInfinity;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            XYZ local = new XYZ(x == 0 ? box.Min.X : box.Max.X, y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z);
                            XYZ model = transform.OfPoint(local);
                            double u = model.DotProduct(right);
                            double v = model.DotProduct(up);
                            left = Math.Min(left, u);
                            rightValue = Math.Max(rightValue, u);
                            bottom = Math.Min(bottom, v);
                            top = Math.Max(top, v);
                        }
                    }
                }
                bounds = new ProjectedBounds(left, rightValue, top, bottom);
                return bounds.IsValid;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.WriteLine("[K-TOOLS][TextAlign] bounding box projection failed: " + ex);
                return false;
            }
        }

        public static XYZ ToModelDelta(View view, double deltaU, double deltaV)
        {
            XYZ right;
            XYZ up;
            XYZ normal;
            string error;
            if (!TryGetBasis(view, out right, out up, out normal, out error)) return XYZ.Zero;
            return right.Multiply(deltaU).Add(up.Multiply(deltaV));
        }

        public static double CenterV(IEnumerable<TextAlignItem> items)
        {
            return items == null ? 0 : items.Select(item => item.Bounds.CenterV).DefaultIfEmpty().Average();
        }
    }

    public sealed class TextAlignSelectionFilter : ISelectionFilter
    {
        private readonly View _view;

        public TextAlignSelectionFilter(View view)
        {
            _view = view;
        }

        public bool AllowElement(Element elem)
        {
            return TextAlignService.IsSupportedElement(elem) && _view != null;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public static class TextAlignPreflightService
    {
        public static List<TextAlignPreflightResult> Run(Document doc, View view, IEnumerable<ElementId> elementIds, AlignType operation)
        {
            var results = new List<TextAlignPreflightResult>();
            bool viewSupported = ViewPlaneGeometry.TryGetBasis(view, out _, out _, out _, out string viewError);
            foreach (ElementId id in (elementIds ?? Enumerable.Empty<ElementId>()).Distinct())
            {
                Element element = id == null || doc == null ? null : doc.GetElement(id);
                var row = new TextAlignPreflightResult
                {
                    ElementId = id ?? ElementId.InvalidElementId,
                    Operation = operation,
                    Severity = 1,
                    Status = TextAlignStatusCode.READY,
                    Message = "Ready.",
                    CanExecute = false
                };
                if (element == null)
                {
                    row.Status = TextAlignStatusCode.MISSING_ELEMENT;
                    row.Message = "Element no longer exists.";
                    results.Add(row);
                    continue;
                }
                TextAlignElementKind kind;
                if (!TextAlignService.TryClassifyElement(element, out kind))
                {
                    row.ElementKind = TextAlignElementKind.Unsupported;
                    row.Status = TextAlignStatusCode.UNSUPPORTED_ELEMENT;
                    row.Message = "Only TextNote, IndependentTag, Generic Annotation, and Detail Item elements are supported.";
                    results.Add(row);
                    continue;
                }
                row.ElementKind = kind;
                if (!viewSupported)
                {
                    row.Status = TextAlignStatusCode.UNSUPPORTED_VIEW;
                    row.Message = viewError;
                    results.Add(row);
                    continue;
                }
                ProjectedBounds bounds;
                string boundsError;
                if (!ViewPlaneGeometry.TryProjectBoundingBox(element, view, out bounds, out boundsError))
                {
                    row.Status = TextAlignStatusCode.NO_BOUNDING_BOX;
                    row.Message = boundsError;
                    results.Add(row);
                    continue;
                }
                var item = new TextAlignItem
                {
                    ElementId = element.Id,
                    ElementKind = kind,
                    ViewId = view.Id,
                    Bounds = bounds,
                    Pinned = element.Pinned,
                    Supported = true
                };
                row.Item = item;
                if (doc.IsReadOnly)
                {
                    row.Status = TextAlignStatusCode.NOT_MOVABLE;
                    row.Message = "The active document is read-only.";
                }
                else if (element.Pinned)
                {
                    row.Status = TextAlignStatusCode.PINNED;
                    row.Message = "Pinned elements are not moved automatically.";
                }
                else if (element.GroupId != null && element.GroupId != ElementId.InvalidElementId)
                {
                    row.Status = TextAlignStatusCode.CONSTRAINED;
                    row.Message = "Grouped elements are not moved automatically.";
                }
                else
                {
                    row.Status = TextAlignStatusCode.READY;
                    row.Message = "Ready.";
                    row.CanExecute = true;
                }
                results.Add(row);
            }

            List<TextAlignPreflightResult> ready = results.Where(row => row.CanExecute).ToList();
            if ((operation == AlignType.HorizontalEquals || operation == AlignType.VerticalEquals) && ready.Count < 3)
            {
                foreach (TextAlignPreflightResult row in ready)
                {
                    row.CanExecute = false;
                    row.Status = TextAlignStatusCode.NOT_ENOUGH_TARGETS;
                    row.Message = "Distribution requires at least three movable supported targets.";
                }
            }
            return results;
        }
    }

    public static class TextAlignService
    {
        public static bool IsSupportedElement(Element element)
        {
            TextAlignElementKind kind;
            return TryClassifyElement(element, out kind);
        }

        public static bool TryClassifyElement(Element element, out TextAlignElementKind kind)
        {
            kind = TextAlignElementKind.Unsupported;
            if (element is TextNote)
            {
                kind = TextAlignElementKind.TextNote;
                return true;
            }
            if (element is IndependentTag)
            {
                kind = TextAlignElementKind.IndependentTag;
                return true;
            }
            var family = element as FamilyInstance;
            if (family == null || family.Category == null) return false;
            BuiltInCategory category = (BuiltInCategory)family.Category.Id.IntegerValue;
            if (category == BuiltInCategory.OST_GenericAnnotation)
            {
                kind = TextAlignElementKind.GenericAnnotation;
                return true;
            }
            if (category == BuiltInCategory.OST_DetailComponents)
            {
                kind = TextAlignElementKind.DetailItem;
                return true;
            }
            return false;
        }

        public static TextAlignBatchResult AlignSelectedElements(UIDocument uidoc, AlignType alignType)
        {
            var batch = new TextAlignBatchResult();
            if (uidoc == null || uidoc.Document == null)
            {
                batch.Cancelled = true;
                batch.Message = "No active Revit document.";
                return batch;
            }
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;
            if (!ViewPlaneGeometry.TryGetBasis(view, out _, out _, out _, out string viewError))
            {
                batch.Cancelled = true;
                batch.Message = viewError;
                return batch;
            }

            List<ElementId> selectedIds = uidoc.Selection.GetElementIds().Distinct().ToList();
            if (selectedIds.Count < 2)
            {
                try
                {
                    IList<Reference> references = uidoc.Selection.PickObjects(ObjectType.Element,
                        new TextAlignSelectionFilter(view),
                        LanguageManager.IsEnglish ? "Select supported annotation elements to align (ESC to cancel)" : "Chọn các đối tượng chú thích được hỗ trợ (Nhấn ESC để hủy)");
                    selectedIds = references.Select(reference => reference.ElementId).Distinct().ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    batch.Cancelled = true;
                    batch.Message = "Selection cancelled.";
                    return batch;
                }
                catch (Exception ex)
                {
                    batch.Failed++;
                    batch.Message = ex.Message;
                    Debug.WriteLine("[K-TOOLS][TextAlign] selection failed: " + ex);
                    return batch;
                }
            }

            batch.Selected = selectedIds.Count;
            if (selectedIds.Count < 2)
            {
                batch.Message = "At least two elements are required.";
                return batch;
            }

            List<TextAlignPreflightResult> preflight = TextAlignPreflightService.Run(doc, view, selectedIds, alignType);
            batch.Preflight.AddRange(preflight);
            batch.Ready = preflight.Count(row => row.CanExecute);
            if (batch.Ready == 0)
            {
                batch.Message = "No supported movable annotation targets are ready.";
                batch.Skipped = preflight.Count(row => !row.CanExecute);
                batch.Results.AddRange(preflight.Select(ToBlockedResult));
                return batch;
            }

            var geometry = preflight.Where(row => row.CanExecute && row.Item != null).Select(row => row.Item).ToList();
            Dictionary<ElementId, ProjectedBounds> targets = ComputeTargets(geometry, alignType, TextAlignReferenceStrategy.AUTO_EXTREME);
            bool distribution = alignType == AlignType.HorizontalEquals || alignType == AlignType.VerticalEquals;
            bool distributionFailed = false;
            try
            {
                using (var group = new TransactionGroup(doc, "K-TOOLS Text Align 2.0"))
                {
                    KhimTools.Core.Revit.TransactionBoundary.Start(group, "TextAlign.Batch");
                    try
                    {
                    foreach (TextAlignPreflightResult row in preflight)
                    {
                        if (!row.CanExecute)
                        {
                            batch.Results.Add(ToBlockedResult(row));
                            continue;
                        }
                        Element live = doc.GetElement(row.ElementId);
                        TextAlignExecutionResult execution = CreateExecution(row, alignType);
                        try
                        {
                            if (live == null)
                            {
                                execution.Status = TextAlignStatusCode.MISSING_ELEMENT;
                                execution.Message = "Element no longer exists.";
                                batch.Results.Add(execution);
                                if (distribution) distributionFailed = true;
                                continue;
                            }
                            if (live.Pinned)
                            {
                                execution.Status = TextAlignStatusCode.PINNED;
                                execution.Message = "Pinned elements are not moved automatically.";
                                batch.Results.Add(execution);
                                if (distribution) distributionFailed = true;
                                continue;
                            }
                            ProjectedBounds liveBounds;
                            string liveError;
                            if (!ViewPlaneGeometry.TryProjectBoundingBox(live, view, out liveBounds, out liveError))
                            {
                                execution.Status = TextAlignStatusCode.NO_BOUNDING_BOX;
                                execution.Message = liveError;
                                batch.Results.Add(execution);
                                if (distribution) distributionFailed = true;
                                continue;
                            }
                            ProjectedBounds target;
                            if (!targets.TryGetValue(row.ElementId, out target))
                            {
                                execution.Status = TextAlignStatusCode.FAILED;
                                execution.Message = "No alignment target was calculated.";
                                batch.Results.Add(execution);
                                if (distribution) distributionFailed = true;
                                continue;
                            }
                            double deltaU = TargetDeltaU(liveBounds, target, alignType);
                            double deltaV = TargetDeltaV(liveBounds, target, alignType);
                            execution.OldCenter = new XYZ(liveBounds.CenterU, liveBounds.CenterV, 0);
                            execution.TargetCenter = new XYZ(target.CenterU, target.CenterV, 0);
                            if (Math.Abs(deltaU) <= ViewPlaneGeometry.Tolerance && Math.Abs(deltaV) <= ViewPlaneGeometry.Tolerance)
                            {
                                execution.Status = TextAlignStatusCode.NO_CHANGE;
                                execution.Message = "Already aligned within tolerance.";
                                batch.Results.Add(execution);
                                continue;
                            }
                            using (var tx = new Transaction(doc, "Align " + alignType))
                            {
                                try
                                {
                                    KhimTools.Core.Revit.TransactionBoundary.Start(tx, "TextAlign." + alignType);
                                    ElementTransformUtils.MoveElement(doc, row.ElementId, ViewPlaneGeometry.ToModelDelta(view, deltaU, deltaV));
                                    doc.Regenerate();
                                    Element moved = doc.GetElement(row.ElementId);
                                    ProjectedBounds movedBounds;
                                    string movedError = null;
                                    if (moved == null || !ViewPlaneGeometry.TryProjectBoundingBox(moved, view, out movedBounds, out movedError) ||
                                        Math.Abs(movedBounds.CenterU - target.CenterU) > ViewPlaneGeometry.Tolerance ||
                                        Math.Abs(movedBounds.CenterV - target.CenterV) > ViewPlaneGeometry.Tolerance)
                                        throw new InvalidOperationException("Text alignment postcondition failed: " + (movedError ?? "target position was not achieved."));
                                    KhimTools.Core.Revit.TransactionBoundary.Commit(tx, "TextAlign." + alignType);
                                }
                                catch
                                {
                                    KhimTools.Core.Revit.TransactionBoundary.RollBack(tx, "TextAlign." + alignType);
                                    throw;
                                }
                            }
                            execution.Changed = true;
                            execution.Status = TextAlignStatusCode.READY;
                            execution.Message = "Moved.";
                            batch.Results.Add(execution);
                        }
                        catch (Exception ex)
                        {
                            execution.Status = TextAlignStatusCode.FAILED;
                            execution.Message = ex.Message;
                            batch.Results.Add(execution);
                            Debug.WriteLine("[K-TOOLS][TextAlign] move failed for " + row.ElementId + ": " + ex);
                            if (distribution) distributionFailed = true;
                        }
                    }
                    if (distribution && distributionFailed)
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "TextAlign.Batch");
                        foreach (TextAlignExecutionResult result in batch.Results.Where(result => result.Changed))
                        {
                            result.Changed = false;
                            result.Status = TextAlignStatusCode.FAILED;
                            result.Message = "Distribution rolled back because the group could not complete coherently.";
                        }
                    }
                    else
                    {
                        KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "TextAlign.Batch");
                    }
                    }
                    catch
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "TextAlign.Batch");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                batch.Message = ex.Message;
                Debug.WriteLine("[K-TOOLS][TextAlign] transaction group failed: " + ex);
                foreach (TextAlignExecutionResult rolledBack in batch.Results.Where(result => result.Changed))
                {
                    rolledBack.Changed = false;
                    rolledBack.Status = TextAlignStatusCode.FAILED;
                    rolledBack.Message = "Alignment group rolled back: " + ex.Message;
                }
                HashSet<ElementId> reported = new HashSet<ElementId>(batch.Results.Select(result => result.ElementId));
                foreach (TextAlignPreflightResult row in preflight.Where(row => row.CanExecute && !reported.Contains(row.ElementId)))
                {
                    batch.Results.Add(new TextAlignExecutionResult
                    {
                        ElementId = row.ElementId,
                        ElementKind = row.ElementKind,
                        Operation = alignType,
                        Status = TextAlignStatusCode.FAILED,
                        Changed = false,
                        Message = ex.Message
                    });
                }
            }

            Recount(batch);
            Debug.WriteLine(string.Format("[K-TOOLS][TextAlign] operation={0}; activeView={1}; selected={2}; supported={3}; moved={4}; noChange={5}; skipped={6}; failed={7}",
                alignType, view.Name, batch.Selected, batch.Ready, batch.Changed, batch.AlreadyAligned, batch.Skipped, batch.Failed));
            try { uidoc.RefreshActiveView(); }
            catch (Exception ex) { Debug.WriteLine("[K-TOOLS][TextAlign] refresh failed: " + ex); }
            return batch;
        }

        public static void ShowSummary(TextAlignBatchResult batch, AlignType alignType)
        {
            if (batch == null || batch.Cancelled) return;
            string text = LanguageManager.IsEnglish
                ? string.Format("Text Align {0}\n\nSelected: {1}\nReady: {2}\nMoved: {3}\nAlready aligned: {4}\nSkipped: {5}\nFailed: {6}", alignType, batch.Selected, batch.Ready, batch.Changed, batch.AlreadyAligned, batch.Skipped, batch.Failed)
                : string.Format("Căn chỉnh Text {0}\n\nĐã chọn: {1}\nSẵn sàng: {2}\nĐã di chuyển: {3}\nĐã đúng vị trí: {4}\nBỏ qua: {5}\nLỗi: {6}", alignType, batch.Selected, batch.Ready, batch.Changed, batch.AlreadyAligned, batch.Skipped, batch.Failed);
            TaskDialog.Show("Khim Tools — Text Align", text);
        }

        public static Dictionary<ElementId, ProjectedBounds> ComputeTargets(IEnumerable<TextAlignItem> source, AlignType operation, TextAlignReferenceStrategy strategy = TextAlignReferenceStrategy.AUTO_EXTREME)
        {
            List<TextAlignItem> items = source == null ? new List<TextAlignItem>() : source.Where(item => item != null).ToList();
            var targets = new Dictionary<ElementId, ProjectedBounds>();
            if (items == null || items.Count == 0) return targets;
            if (operation == AlignType.HorizontalEquals)
            {
                List<TextAlignItem> sorted = items.OrderBy(item => item.Bounds.CenterU).ThenBy(item => item.ElementId.IntegerValue).ToList();
                double first = sorted.First().Bounds.CenterU;
                double last = sorted.Last().Bounds.CenterU;
                for (int i = 0; i < sorted.Count; i++)
                {
                    double center = first + (last - first) * i / (sorted.Count - 1);
                    targets[sorted[i].ElementId] = CenterTarget(sorted[i].Bounds, center, sorted[i].Bounds.CenterV);
                }
                return targets;
            }
            if (operation == AlignType.VerticalEquals)
            {
                List<TextAlignItem> sorted = items.OrderByDescending(item => item.Bounds.CenterV).ThenBy(item => item.ElementId.IntegerValue).ToList();
                double first = sorted.First().Bounds.CenterV;
                double last = sorted.Last().Bounds.CenterV;
                for (int i = 0; i < sorted.Count; i++)
                {
                    double center = first - (first - last) * i / (sorted.Count - 1);
                    targets[sorted[i].ElementId] = CenterTarget(sorted[i].Bounds, sorted[i].Bounds.CenterU, center);
                }
                return targets;
            }

            double targetLeft = items.Min(item => item.Bounds.Left);
            double targetRight = items.Max(item => item.Bounds.Right);
            double targetTop = items.Max(item => item.Bounds.Top);
            double targetBottom = items.Min(item => item.Bounds.Bottom);
            double targetCenterV = items.Average(item => item.Bounds.CenterV);
            foreach (TextAlignItem item in items)
            {
                ProjectedBounds bounds = item.Bounds;
                if (operation == AlignType.Left) bounds = CenterTarget(bounds, targetLeft + bounds.Width / 2.0, bounds.CenterV);
                else if (operation == AlignType.Right) bounds = CenterTarget(bounds, targetRight - bounds.Width / 2.0, bounds.CenterV);
                else if (operation == AlignType.Top) bounds = CenterTarget(bounds, bounds.CenterU, targetTop - bounds.Height / 2.0);
                else if (operation == AlignType.Bottom) bounds = CenterTarget(bounds, bounds.CenterU, targetBottom + bounds.Height / 2.0);
                else if (operation == AlignType.Middle) bounds = CenterTarget(bounds, bounds.CenterU, targetCenterV);
                targets[item.ElementId] = bounds;
            }
            return targets;
        }

        public static ProjectedBounds CenterTarget(ProjectedBounds source, double centerU, double centerV)
        {
            return new ProjectedBounds(centerU - source.Width / 2.0, centerU + source.Width / 2.0, centerV + source.Height / 2.0, centerV - source.Height / 2.0);
        }

        public static double TargetDeltaU(ProjectedBounds current, ProjectedBounds target, AlignType operation)
        {
            if (operation == AlignType.Left) return target.Left - current.Left;
            if (operation == AlignType.Right) return target.Right - current.Right;
            if (operation == AlignType.HorizontalEquals) return target.CenterU - current.CenterU;
            return 0;
        }

        public static double TargetDeltaV(ProjectedBounds current, ProjectedBounds target, AlignType operation)
        {
            if (operation == AlignType.Top) return target.Top - current.Top;
            if (operation == AlignType.Bottom) return target.Bottom - current.Bottom;
            if (operation == AlignType.Middle || operation == AlignType.VerticalEquals) return target.CenterV - current.CenterV;
            return 0;
        }

        private static TextAlignExecutionResult CreateExecution(TextAlignPreflightResult row, AlignType operation)
        {
            return new TextAlignExecutionResult
            {
                ElementId = row.ElementId,
                ElementKind = row.ElementKind,
                Operation = operation,
                Status = row.Status,
                Message = row.Message
            };
        }

        private static TextAlignExecutionResult ToBlockedResult(TextAlignPreflightResult row)
        {
            return new TextAlignExecutionResult
            {
                ElementId = row.ElementId,
                ElementKind = row.ElementKind,
                Operation = row.Operation,
                Status = row.Status,
                Changed = false,
                Message = row.Message
            };
        }

        private static void Recount(TextAlignBatchResult batch)
        {
            batch.Changed = batch.Results.Count(result => result.Changed);
            batch.AlreadyAligned = batch.Results.Count(result => result.Status == TextAlignStatusCode.NO_CHANGE);
            batch.Skipped = batch.Results.Count(result => !result.Changed && result.Status != TextAlignStatusCode.NO_CHANGE && result.Status != TextAlignStatusCode.FAILED && result.Status != TextAlignStatusCode.READY);
            batch.Failed = batch.Results.Count(result => result.Status == TextAlignStatusCode.FAILED);
        }
    }
}
