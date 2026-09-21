using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;

namespace KhimTools.ViewportAlign.Services
{
    /// <summary>Legacy modes retained for callers from the first Align Viewports release.</summary>
    public enum ArrangeMode
    {
        ViewsAndTitles,
        ViewsOnly,
        TitlesOnly
    }

    public enum AlignmentReferenceType
    {
        VIEWPORT,
        SCHEDULE
    }

    public enum AlignmentTargetKind
    {
        Viewport,
        LegendViewport,
        Schedule
    }

    public enum AlignmentOperation
    {
        MATCH_CENTER,
        ALIGN_LEFT,
        ALIGN_RIGHT,
        ALIGN_TOP,
        ALIGN_BOTTOM,
        ALIGN_CENTER_X,
        ALIGN_CENTER_Y,
        MATCH_TITLE,
        MATCH_VIEW_AND_TITLE,
        MATCH_POSITION,
        DISTRIBUTE_HORIZONTAL,
        DISTRIBUTE_VERTICAL
    }

    public enum AlignmentStatusCode
    {
        READY,
        NO_CHANGE,
        SOURCE_REFERENCE,
        INVALID_TARGET,
        INCOMPATIBLE_REFERENCE_TYPE,
        PINNED,
        READ_ONLY,
        NOT_MOVABLE,
        UNSUPPORTED_OPERATION,
        TITLE_UPDATED,
        TITLE_UNSUPPORTED,
        TITLE_READ_ONLY,
        TITLE_FAILED,
        MISSING_ELEMENT,
        FAILED
    }

    public enum AlignmentExecutionStatus
    {
        SUCCESS_CHANGED,
        SUCCESS_NO_CHANGE,
        SKIPPED,
        BLOCKED,
        FAILED
    }

    /// <summary>Sheet-space bounds. Revit internal feet are used deliberately.</summary>
    public struct SheetBounds
    {
        public double Left;
        public double Right;
        public double Bottom;
        public double Top;

        public bool IsValid => Right >= Left && Top >= Bottom;
        public double Width => Math.Max(0, Right - Left);
        public double Height => Math.Max(0, Top - Bottom);
        public double CenterX => (Left + Right) / 2.0;
        public double CenterY => (Bottom + Top) / 2.0;
        public bool HasDimensions => Width > 0.000001 && Height > 0.000001;

        public SheetBounds(double left, double right, double bottom, double top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }

        public XYZ Center => new XYZ(CenterX, CenterY, 0);
    }

    public sealed class AlignmentReference
    {
        public AlignmentReferenceType ReferenceType { get; private set; }
        public ElementId SheetId { get; private set; }
        public ElementId ElementId { get; private set; }
        public ElementId ViewId { get; private set; }
        public ElementId ScheduleId { get; private set; }
        public XYZ Position { get; private set; }
        public SheetBounds BoundingBox { get; private set; }
        public ViewType ViewType { get; private set; }
        public string Name { get; private set; }

        private AlignmentReference() { }

        public static AlignmentReference FromViewport(Viewport viewport, View view, ViewSheet sheet, SheetBounds bounds)
        {
            if (viewport == null) return null;
            return new AlignmentReference
            {
                ReferenceType = AlignmentReferenceType.VIEWPORT,
                SheetId = viewport.SheetId,
                ElementId = viewport.Id,
                ViewId = viewport.ViewId,
                ScheduleId = ElementId.InvalidElementId,
                Position = viewport.GetBoxCenter(),
                BoundingBox = bounds,
                ViewType = view?.ViewType ?? ViewType.Undefined,
                Name = view?.Name ?? viewport.Id.IntegerValue.ToString()
            };
        }

        public static AlignmentReference FromSchedule(ScheduleSheetInstance schedule, ViewSchedule view, ViewSheet sheet, SheetBounds bounds)
        {
            if (schedule == null) return null;
            return new AlignmentReference
            {
                ReferenceType = AlignmentReferenceType.SCHEDULE,
                SheetId = schedule.OwnerViewId,
                ElementId = schedule.Id,
                ViewId = ElementId.InvalidElementId,
                ScheduleId = schedule.ScheduleId,
                Position = schedule.Point,
                BoundingBox = bounds,
                ViewType = ViewType.Schedule,
                Name = view?.Name ?? schedule.ScheduleId.IntegerValue.ToString()
            };
        }
    }

    public class ViewportAlignOptions
    {
        public ArrangeMode Mode { get; set; } = ArrangeMode.ViewsAndTitles;
        public AlignmentOperation Operation { get; set; } = AlignmentOperation.MATCH_VIEW_AND_TITLE;
        public bool AlignModelViews { get; set; } = true;
        public bool AlignDraftingViews { get; set; } = true;
        public bool AlignLegends { get; set; } = false;
        public bool AlignSchedules { get; set; } = true;
        public string KeywordFilter { get; set; } = "";
    }

    public class TargetViewItem
    {
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public ElementId ViewId { get; set; }
        public ElementId ViewportOrScheduleId { get; set; }
        public string ViewName { get; set; }
        public ViewType ViewType { get; set; }
        public AlignmentTargetKind TargetKind { get; set; }
        public bool IsSchedule { get; set; }
        public bool IsPinned { get; set; }
        public bool IsReadOnly { get; set; }

        public bool IsViewport => TargetKind == AlignmentTargetKind.Viewport || TargetKind == AlignmentTargetKind.LegendViewport;
    }

    public sealed class AlignmentPreflightResult
    {
        public ElementId TargetId { get; set; }
        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string TargetName { get; set; }
        public AlignmentTargetKind TargetType { get; set; }
        public AlignmentOperation Operation { get; set; }
        public int Severity { get; set; }
        public AlignmentStatusCode StatusCode { get; set; }
        public string Message { get; set; }
        public bool CanExecute { get; set; }
    }

    public sealed class AlignmentExecutionResult
    {
        public ElementId TargetElementId { get; set; }
        public string SheetNumber { get; set; }
        public string TargetName { get; set; }
        public AlignmentOperation Operation { get; set; }
        public AlignmentExecutionStatus Status { get; set; }
        public AlignmentStatusCode StatusCode { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; }
    }

    public sealed class AlignmentBatchSummary
    {
        public int Requested { get; set; }
        public int Ready { get; set; }
        public int Changed { get; set; }
        public int AlreadyAligned { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }

        public void Add(AlignmentExecutionResult result)
        {
            if (result == null) return;
            if (result.Status == AlignmentExecutionStatus.SUCCESS_CHANGED) Changed++;
            else if (result.Status == AlignmentExecutionStatus.SUCCESS_NO_CHANGE) AlreadyAligned++;
            else if (result.Status == AlignmentExecutionStatus.SKIPPED || result.Status == AlignmentExecutionStatus.BLOCKED) Skipped++;
            else if (result.Status == AlignmentExecutionStatus.FAILED) Failed++;
        }
    }

    /// <summary>Pure sheet-space calculations kept independent from Revit transactions.</summary>
    public static class ViewportAlignmentGeometry
    {
        public static XYZ CalculateTargetCenter(SheetBounds reference, SheetBounds target, AlignmentOperation operation)
        {
            if (!reference.IsValid || !target.IsValid) return target.Center;
            double x = target.CenterX;
            double y = target.CenterY;
            switch (operation)
            {
                case AlignmentOperation.MATCH_CENTER:
                case AlignmentOperation.MATCH_POSITION:
                case AlignmentOperation.MATCH_VIEW_AND_TITLE:
                    x = reference.CenterX;
                    y = reference.CenterY;
                    break;
                case AlignmentOperation.ALIGN_LEFT:
                    x = reference.Left + target.Width / 2.0;
                    break;
                case AlignmentOperation.ALIGN_RIGHT:
                    x = reference.Right - target.Width / 2.0;
                    break;
                case AlignmentOperation.ALIGN_TOP:
                    y = reference.Top - target.Height / 2.0;
                    break;
                case AlignmentOperation.ALIGN_BOTTOM:
                    y = reference.Bottom + target.Height / 2.0;
                    break;
                case AlignmentOperation.ALIGN_CENTER_X:
                    x = reference.CenterX;
                    break;
                case AlignmentOperation.ALIGN_CENTER_Y:
                    y = reference.CenterY;
                    break;
            }
            return new XYZ(x, y, target.Center.Z);
        }

        public static double[] Distribute(double first, double last, int count)
        {
            if (count < 3) return new double[0];
            double[] coordinates = new double[count];
            double step = (last - first) / (count - 1);
            for (int index = 0; index < count; index++) coordinates[index] = first + step * index;
            return coordinates;
        }
    }

    public static class ViewportAlignmentCollector
    {
        public static List<TargetViewItem> Collect(Document doc, ViewSheet sheet)
        {
            return ViewportAlignService.GetViewsOnSheet(doc, sheet);
        }
    }

    public static class ViewportAlignmentPreflightService
    {
        public static List<AlignmentPreflightResult> Run(Document doc, AlignmentReference source, IEnumerable<TargetViewItem> targets, AlignmentOperation operation)
        {
            return ViewportAlignService.Preflight(doc, source, targets, operation);
        }
    }

    public static class ViewportAlignmentExecutor
    {
        public static AlignmentExecutionResult Execute(Document doc, AlignmentReference source, TargetViewItem target, AlignmentOperation operation, XYZ distributionCenter = null)
        {
            return ViewportAlignService.ExecuteTarget(doc, source, target, operation, distributionCenter);
        }
    }

    public static class ViewportAlignService
    {
        /// Revit sheet coordinates are internal feet. This tolerance is intentionally centralized.
        public const double SheetCoordinateTolerance = 0.001;

        public static AlignmentOperation OperationFor(ArrangeMode mode)
        {
            if (mode == ArrangeMode.ViewsOnly) return AlignmentOperation.MATCH_CENTER;
            if (mode == ArrangeMode.TitlesOnly) return AlignmentOperation.MATCH_TITLE;
            return AlignmentOperation.MATCH_VIEW_AND_TITLE;
        }

        public static SheetBounds GetViewportBounds(Viewport viewport, ViewSheet sheet)
        {
            if (viewport == null) return new SheetBounds();
            try
            {
                Outline outline = viewport.GetBoxOutline();
                if (outline != null)
                    return new SheetBounds(outline.MinimumPoint.X, outline.MaximumPoint.X, outline.MinimumPoint.Y, outline.MaximumPoint.Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[ViewportAlign] viewport outline unavailable: " + ex.Message);
            }
            return GetBoundingBox(viewport, sheet, viewport.GetBoxCenter());
        }

        public static SheetBounds GetScheduleBounds(ScheduleSheetInstance schedule, ViewSheet sheet)
        {
            if (schedule == null) return new SheetBounds();
            return GetBoundingBox(schedule, sheet, schedule.Point);
        }

        private static SheetBounds GetBoundingBox(Element element, ViewSheet sheet, XYZ fallback)
        {
            try
            {
                BoundingBoxXYZ box = element?.get_BoundingBox(sheet);
                if (box != null && box.Min != null && box.Max != null)
                    return new SheetBounds(box.Min.X, box.Max.X, box.Min.Y, box.Max.Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[ViewportAlign] element bounds unavailable: " + ex.Message);
            }

            XYZ point = fallback ?? XYZ.Zero;
            return new SheetBounds(point.X, point.X, point.Y, point.Y);
        }

        public static AlignmentReference CreateViewportReference(Document doc, Viewport viewport)
        {
            if (doc == null || viewport == null) return null;
            var view = doc.GetElement(viewport.ViewId) as View;
            var sheet = doc.GetElement(viewport.SheetId) as ViewSheet;
            return AlignmentReference.FromViewport(viewport, view, sheet, GetViewportBounds(viewport, sheet));
        }

        public static AlignmentReference CreateScheduleReference(Document doc, ScheduleSheetInstance schedule)
        {
            if (doc == null || schedule == null) return null;
            var view = doc.GetElement(schedule.ScheduleId) as ViewSchedule;
            var sheet = doc.GetElement(schedule.OwnerViewId) as ViewSheet;
            return AlignmentReference.FromSchedule(schedule, view, sheet, GetScheduleBounds(schedule, sheet));
        }

        public static XYZ CalculateAlignedCenter(SheetBounds reference, SheetBounds target, AlignmentOperation operation)
        {
            return ViewportAlignmentGeometry.CalculateTargetCenter(reference, target, operation);
        }

        public static bool CentersEqual(XYZ left, XYZ right)
        {
            return left != null && right != null && (left - right).GetLength() <= SheetCoordinateTolerance;
        }

        public static List<TargetViewItem> GetViewsOnSheet(Document doc, ViewSheet sheet)
        {
            var result = new List<TargetViewItem>();
            if (doc == null || sheet == null) return result;

            foreach (ElementId viewportId in sheet.GetAllViewports())
            {
                var viewport = doc.GetElement(viewportId) as Viewport;
                var view = viewport == null ? null : doc.GetElement(viewport.ViewId) as View;
                if (viewport == null || view == null) continue;
                AlignmentTargetKind kind = view.ViewType == ViewType.Legend ? AlignmentTargetKind.LegendViewport : AlignmentTargetKind.Viewport;
                result.Add(new TargetViewItem
                {
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber ?? "",
                    SheetName = sheet.Name ?? "",
                    ViewId = view.Id,
                    ViewportOrScheduleId = viewport.Id,
                    ViewName = view.Name ?? "",
                    ViewType = view.ViewType,
                    TargetKind = kind,
                    IsSchedule = false,
                    IsPinned = viewport.Pinned,
                    IsReadOnly = false
                });
            }

            foreach (var schedule in new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>().OrderBy(s => s.Id.IntegerValue))
            {
                var scheduleView = doc.GetElement(schedule.ScheduleId) as ViewSchedule;
                result.Add(new TargetViewItem
                {
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber ?? "",
                    SheetName = sheet.Name ?? "",
                    ViewId = scheduleView?.Id ?? ElementId.InvalidElementId,
                    ViewportOrScheduleId = schedule.Id,
                    ViewName = scheduleView?.Name ?? (LanguageManager.IsEnglish ? "Schedule" : "Bảng thống kê"),
                    ViewType = ViewType.Schedule,
                    TargetKind = AlignmentTargetKind.Schedule,
                    IsSchedule = true,
                    IsPinned = schedule.Pinned,
                    IsReadOnly = false
                });
            }
            return result;
        }

        public static List<AlignmentPreflightResult> Preflight(Document doc, AlignmentReference source, IEnumerable<TargetViewItem> targets, AlignmentOperation operation)
        {
            var results = new List<AlignmentPreflightResult>();
            if (targets == null) return results;
            var targetList = targets.ToList();
            bool distribution = operation == AlignmentOperation.DISTRIBUTE_HORIZONTAL || operation == AlignmentOperation.DISTRIBUTE_VERTICAL;
            foreach (var item in targetList)
            {
                var result = new AlignmentPreflightResult
                {
                    TargetId = item?.ViewportOrScheduleId ?? ElementId.InvalidElementId,
                    SheetId = item?.SheetId ?? ElementId.InvalidElementId,
                    SheetNumber = item?.SheetNumber ?? "",
                    TargetName = item?.ViewName ?? "",
                    TargetType = item?.TargetKind ?? AlignmentTargetKind.Viewport,
                    Operation = operation,
                    Severity = 0,
                    StatusCode = AlignmentStatusCode.READY,
                    Message = "Ready",
                    CanExecute = true
                };

                if (item == null || item.ViewportOrScheduleId == null || item.ViewportOrScheduleId == ElementId.InvalidElementId)
                    SetBlocked(result, AlignmentStatusCode.INVALID_TARGET, "Target is invalid.");
                else if (doc == null || doc.GetElement(item.ViewportOrScheduleId) == null)
                    SetBlocked(result, AlignmentStatusCode.MISSING_ELEMENT, "Target no longer exists.");
                else if (source == null)
                    SetBlocked(result, AlignmentStatusCode.MISSING_ELEMENT, "Alignment reference is missing.");
                else if (item.TargetKind == AlignmentTargetKind.Schedule && !(doc.GetElement(item.ViewportOrScheduleId) is ScheduleSheetInstance))
                    SetBlocked(result, AlignmentStatusCode.NOT_MOVABLE, "Target is not a movable schedule instance.");
                else if (item.TargetKind != AlignmentTargetKind.Schedule && !(doc.GetElement(item.ViewportOrScheduleId) is Viewport))
                    SetBlocked(result, AlignmentStatusCode.NOT_MOVABLE, "Target is not a movable viewport.");
                else if (!IsCurrentSheetMember(doc.GetElement(item.ViewportOrScheduleId), item.SheetId))
                    SetBlocked(result, AlignmentStatusCode.INVALID_TARGET, "Target is no longer on the selected sheet.");
                else if (item.ViewportOrScheduleId == source.ElementId)
                    SetBlocked(result, AlignmentStatusCode.SOURCE_REFERENCE, "The reference element is excluded from execution.");
                else if (source.ReferenceType == AlignmentReferenceType.SCHEDULE && item.TargetKind != AlignmentTargetKind.Schedule)
                    SetBlocked(result, AlignmentStatusCode.INCOMPATIBLE_REFERENCE_TYPE, "A schedule reference can align schedules only.");
                else if (source.ReferenceType == AlignmentReferenceType.VIEWPORT && item.TargetKind == AlignmentTargetKind.Schedule)
                    SetBlocked(result, AlignmentStatusCode.INCOMPATIBLE_REFERENCE_TYPE, "A viewport reference can align viewports only.");
                else if ((operation == AlignmentOperation.MATCH_TITLE || operation == AlignmentOperation.MATCH_VIEW_AND_TITLE) && item.TargetKind == AlignmentTargetKind.Schedule)
                    SetBlocked(result, AlignmentStatusCode.UNSUPPORTED_OPERATION, "Title operations require viewport targets.");
                else if (source.ReferenceType == AlignmentReferenceType.SCHEDULE && RequiresBounds(operation) && !source.BoundingBox.HasDimensions)
                    SetBlocked(result, AlignmentStatusCode.UNSUPPORTED_OPERATION, "Schedule sheet bounds are unavailable for this operation.");
                else if (item.IsPinned)
                    SetBlocked(result, AlignmentStatusCode.PINNED, "Pinned targets are not moved automatically.");
                else if (item.IsReadOnly)
                    SetBlocked(result, AlignmentStatusCode.READ_ONLY, "Target is read-only.");
                else if (doc != null && doc.IsReadOnly)
                    SetBlocked(result, AlignmentStatusCode.READ_ONLY, "The active document is read-only.");
                else if (distribution && targetList.Count(t => t != null && t.TargetKind == item.TargetKind && !t.IsPinned) < 3)
                    SetBlocked(result, AlignmentStatusCode.UNSUPPORTED_OPERATION, "Distribution requires at least three compatible targets.");

                results.Add(result);
            }
            return results;
        }

        private static bool RequiresBounds(AlignmentOperation operation)
        {
            return operation == AlignmentOperation.ALIGN_LEFT || operation == AlignmentOperation.ALIGN_RIGHT ||
                   operation == AlignmentOperation.ALIGN_TOP || operation == AlignmentOperation.ALIGN_BOTTOM ||
                   operation == AlignmentOperation.DISTRIBUTE_HORIZONTAL || operation == AlignmentOperation.DISTRIBUTE_VERTICAL;
        }

        private static bool IsCurrentSheetMember(Element element, ElementId expectedSheetId)
        {
            if (element == null || expectedSheetId == null || expectedSheetId == ElementId.InvalidElementId) return false;
            if (element is Viewport viewport) return viewport.SheetId == expectedSheetId;
            if (element is ScheduleSheetInstance schedule) return schedule.OwnerViewId == expectedSheetId;
            return false;
        }

        private static void SetBlocked(AlignmentPreflightResult result, AlignmentStatusCode code, string message)
        {
            result.StatusCode = code;
            result.Message = message;
            result.CanExecute = false;
            result.Severity = 2;
        }

        public static Dictionary<ElementId, XYZ> ComputeDistributionCenters(Document doc, IEnumerable<TargetViewItem> targets, AlignmentOperation operation)
        {
            var rows = new List<Tuple<TargetViewItem, SheetBounds>>();
            foreach (var item in targets ?? Enumerable.Empty<TargetViewItem>())
            {
                var element = doc?.GetElement(item?.ViewportOrScheduleId ?? ElementId.InvalidElementId);
                var sheet = doc?.GetElement(item?.SheetId) as ViewSheet;
                SheetBounds bounds = item != null && item.TargetKind == AlignmentTargetKind.Schedule
                    ? GetScheduleBounds(element as ScheduleSheetInstance, sheet)
                    : GetViewportBounds(element as Viewport, sheet);
                if (item != null && bounds.IsValid) rows.Add(Tuple.Create(item, bounds));
            }
            bool horizontal = operation == AlignmentOperation.DISTRIBUTE_HORIZONTAL;
            var ordered = (horizontal
                ? rows.OrderBy(r => r.Item2.CenterX).ThenBy(r => r.Item1.ViewportOrScheduleId.IntegerValue)
                : rows.OrderBy(r => r.Item2.CenterY).ThenBy(r => r.Item1.ViewportOrScheduleId.IntegerValue)).ToList();
            var result = new Dictionary<ElementId, XYZ>();
            if (ordered.Count < 3) return result;
            double first = horizontal ? ordered.First().Item2.CenterX : ordered.First().Item2.CenterY;
            double last = horizontal ? ordered.Last().Item2.CenterX : ordered.Last().Item2.CenterY;
            double[] coordinates = ViewportAlignmentGeometry.Distribute(first, last, ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var current = ordered[i].Item2;
                double coordinate = coordinates[i];
                result[ordered[i].Item1.ViewportOrScheduleId] = horizontal
                    ? new XYZ(coordinate, current.CenterY, current.Center.Z)
                    : new XYZ(current.CenterX, coordinate, current.Center.Z);
            }
            return result;
        }

        public static AlignmentExecutionResult ExecuteTarget(Document doc, AlignmentReference source, TargetViewItem target, AlignmentOperation operation, XYZ distributionCenter = null)
        {
            var result = new AlignmentExecutionResult
            {
                TargetElementId = target?.ViewportOrScheduleId ?? ElementId.InvalidElementId,
                SheetNumber = target?.SheetNumber ?? "",
                TargetName = target?.ViewName ?? "",
                Operation = operation,
                Status = AlignmentExecutionStatus.BLOCKED,
                StatusCode = AlignmentStatusCode.INVALID_TARGET,
                Changed = false,
                Message = "Target is invalid."
            };
            if (doc == null || source == null || target == null) return result;
            if (target.ViewportOrScheduleId == source.ElementId)
            {
                result.StatusCode = AlignmentStatusCode.SOURCE_REFERENCE;
                result.Message = "The reference element was excluded.";
                return result;
            }
            if (source.ReferenceType == AlignmentReferenceType.SCHEDULE && target.TargetKind != AlignmentTargetKind.Schedule ||
                source.ReferenceType == AlignmentReferenceType.VIEWPORT && target.TargetKind == AlignmentTargetKind.Schedule)
            {
                result.StatusCode = AlignmentStatusCode.INCOMPATIBLE_REFERENCE_TYPE;
                result.Message = "Reference and target types are incompatible.";
                return result;
            }
            if (target.IsPinned)
            {
                result.StatusCode = AlignmentStatusCode.PINNED;
                result.Message = "Pinned targets are not moved automatically.";
                return result;
            }

            var sheet = doc.GetElement(target.SheetId) as ViewSheet;
            var element = doc.GetElement(target.ViewportOrScheduleId);
            if (element == null)
            {
                result.Status = AlignmentExecutionStatus.BLOCKED;
                result.StatusCode = AlignmentStatusCode.MISSING_ELEMENT;
                result.Message = "Target no longer exists.";
                return result;
            }

            if (target.TargetKind == AlignmentTargetKind.Schedule)
            {
                if (!(element is ScheduleSheetInstance targetSchedule))
                {
                    result.StatusCode = AlignmentStatusCode.INVALID_TARGET;
                    result.Message = "Target is not a schedule instance.";
                    return result;
                }
                SheetBounds targetBounds = GetScheduleBounds(targetSchedule, sheet);
                if (RequiresBounds(operation) && !targetBounds.HasDimensions)
                {
                    result.StatusCode = AlignmentStatusCode.UNSUPPORTED_OPERATION;
                    result.Message = "Target schedule bounds are unavailable for this operation.";
                    return result;
                }
                XYZ desired = distributionCenter ?? CalculateAlignedCenter(source.BoundingBox, targetBounds, operation);
                XYZ current = targetSchedule.Point;
                if (CentersEqual(current, desired))
                {
                    result.Status = AlignmentExecutionStatus.SUCCESS_NO_CHANGE;
                    result.StatusCode = AlignmentStatusCode.NO_CHANGE;
                    result.Message = "Already aligned.";
                    return result;
                }
                targetSchedule.Point = desired;
                result.Status = AlignmentExecutionStatus.SUCCESS_CHANGED;
                result.StatusCode = AlignmentStatusCode.READY;
                result.Changed = true;
                result.Message = "Schedule position updated.";
                return result;
            }

            if (!(element is Viewport targetViewport) || !(doc.GetElement(source.ElementId) is Viewport sourceViewport))
            {
                result.StatusCode = AlignmentStatusCode.MISSING_ELEMENT;
                result.Message = "Viewport reference or target no longer exists.";
                return result;
            }

            bool wantsTitle = operation == AlignmentOperation.MATCH_TITLE || operation == AlignmentOperation.MATCH_VIEW_AND_TITLE;
            bool wantsView = operation != AlignmentOperation.MATCH_TITLE;
            bool changed = false;
            if (wantsView)
            {
                SheetBounds targetBounds = GetViewportBounds(targetViewport, sheet);
                XYZ desired = distributionCenter ?? CalculateAlignedCenter(source.BoundingBox, targetBounds, operation);
                if (!CentersEqual(targetViewport.GetBoxCenter(), desired))
                {
                    targetViewport.SetBoxCenter(desired);
                    changed = true;
                }
            }
            if (wantsTitle)
            {
                AlignmentStatusCode titleStatus;
                bool titleChanged;
                string titleMessage;
                ApplyTitle(sourceViewport, targetViewport, out titleStatus, out titleChanged, out titleMessage);
                changed |= titleChanged;
                if (titleStatus != AlignmentStatusCode.READY && titleStatus != AlignmentStatusCode.NO_CHANGE && titleStatus != AlignmentStatusCode.TITLE_UPDATED)
                {
                    // View and title form one atomic operation. A title failure must roll back a view move.
                    result.Status = AlignmentExecutionStatus.FAILED;
                    result.StatusCode = titleStatus;
                    result.Changed = false;
                    result.Message = titleMessage;
                    return result;
                }
            }
            result.Changed = changed;
            result.Status = changed ? AlignmentExecutionStatus.SUCCESS_CHANGED : AlignmentExecutionStatus.SUCCESS_NO_CHANGE;
            result.StatusCode = changed ? AlignmentStatusCode.READY : AlignmentStatusCode.NO_CHANGE;
            result.Message = changed ? "Viewport alignment updated." : "Already aligned.";
            return result;
        }

        private static void ApplyTitle(Viewport source, Viewport target, out AlignmentStatusCode status, out bool changed, out string message)
        {
            status = AlignmentStatusCode.READY;
            changed = false;
            message = "Title unchanged.";
            try
            {
                XYZ sourceOffset = source.LabelOffset;
                XYZ targetOffset = target.LabelOffset;
                if (sourceOffset != null && (sourceOffset - targetOffset).GetLength() > SheetCoordinateTolerance)
                {
                    target.LabelOffset = sourceOffset;
                    changed = true;
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                status = AlignmentStatusCode.TITLE_UNSUPPORTED;
                message = "Target title offset is unsupported: " + ex.Message;
                return;
            }
            catch (Exception ex)
            {
                status = ex.GetType().Name.IndexOf("ModificationForbidden", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AlignmentStatusCode.TITLE_READ_ONLY
                    : AlignmentStatusCode.TITLE_FAILED;
                message = "Target title offset failed: " + ex.Message;
                return;
            }

            try
            {
                double sourceLength = source.LabelLineLength;
                if (Math.Abs(sourceLength - target.LabelLineLength) > SheetCoordinateTolerance)
                {
                    target.LabelLineLength = sourceLength;
                    changed = true;
                }
                status = changed ? AlignmentStatusCode.TITLE_UPDATED : AlignmentStatusCode.NO_CHANGE;
                message = changed ? "Title properties updated." : "Title already aligned.";
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                status = AlignmentStatusCode.TITLE_UNSUPPORTED;
                message = "Target title line is unsupported: " + ex.Message;
            }
            catch (Exception ex)
            {
                status = ex.GetType().Name.IndexOf("ModificationForbidden", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AlignmentStatusCode.TITLE_READ_ONLY
                    : AlignmentStatusCode.TITLE_FAILED;
                message = "Target title line failed: " + ex.Message;
            }
        }

        // Compatibility wrappers for existing callers and static checks.
        public static bool AlignViewport(Document doc, Viewport targetVp, Viewport sourceVp, ArrangeMode mode)
        {
            if (doc == null || targetVp == null || sourceVp == null) return false;
            var source = CreateViewportReference(doc, sourceVp);
            var sourceView = doc.GetElement(targetVp.ViewId) as View;
            var item = new TargetViewItem
            {
                SheetId = targetVp.SheetId,
                SheetNumber = "",
                ViewportOrScheduleId = targetVp.Id,
                ViewName = targetVp.Id.IntegerValue.ToString(),
                ViewType = sourceView?.ViewType ?? ViewType.Undefined,
                TargetKind = sourceView?.ViewType == ViewType.Legend ? AlignmentTargetKind.LegendViewport : AlignmentTargetKind.Viewport
            };
            return ExecuteTarget(doc, source, item, OperationFor(mode)).Changed;
        }

        public static bool AlignSchedule(Document doc, ScheduleSheetInstance targetSched, ScheduleSheetInstance sourceSched)
        {
            if (doc == null || targetSched == null || sourceSched == null) return false;
            var source = CreateScheduleReference(doc, sourceSched);
            var item = new TargetViewItem
            {
                SheetId = targetSched.OwnerViewId,
                ViewportOrScheduleId = targetSched.Id,
                ViewName = targetSched.Id.IntegerValue.ToString(),
                TargetKind = AlignmentTargetKind.Schedule,
                IsSchedule = true
            };
            return ExecuteTarget(doc, source, item, AlignmentOperation.MATCH_POSITION).Changed;
        }
    }
}
