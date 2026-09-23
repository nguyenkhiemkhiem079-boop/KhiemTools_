using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ScheduleSplit.Models;

namespace KhimTools.ScheduleSplit.Services
{
    public static class ScheduleSplitExecutionService
    {
        public static ScheduleSplitExecutionResult Execute(Document doc, ScheduleSplitPlan plan, ScheduleSplitOptions options)
        {
            var result = new ScheduleSplitExecutionResult { SourceScheduleId = plan == null ? ElementId.InvalidElementId : plan.SourceScheduleId, WorkingScheduleId = plan == null ? ElementId.InvalidElementId : plan.SourceScheduleId };
            Stopwatch timer = Stopwatch.StartNew();
            if (doc == null || plan == null) { result.Status = ScheduleSplitStatusCode.FAILED; result.Messages.Add("Document or plan is unavailable."); return result; }
            options = options ?? new ScheduleSplitOptions();
            List<ScheduleSplitPreflightResult> diagnostics = ScheduleSplitPreflightService.Validate(doc, plan, options);
            if (diagnostics.Any(d => d.Severity == ScheduleSplitSeverity.ERROR))
            {
                ScheduleSplitPreflightResult firstError = null;
                foreach (ScheduleSplitPreflightResult diagnostic in diagnostics) if (diagnostic.Severity == ScheduleSplitSeverity.ERROR) { firstError = diagnostic; break; }
                result.Status = firstError == null ? ScheduleSplitStatusCode.FAILED : firstError.Status;
                result.Messages.AddRange(diagnostics.Where(d => d.Severity == ScheduleSplitSeverity.ERROR).Select(d => d.Message).Distinct());
                return result;
            }
            using (var group = new TransactionGroup(doc, "K-TOOLS Split Schedule 2.0"))
            {
                KhimTools.Core.Revit.TransactionBoundary.Start(group, "ScheduleSplit batch");
                try
                {
                    ViewSchedule source = doc.GetElement(plan.SourceScheduleId) as ViewSchedule;
                    ViewSchedule working = source;
                    if (options.SourceMode == ScheduleSplitSourceMode.WORKING_COPY)
                    {
                        if (!source.CanViewBeDuplicated(ViewDuplicateOption.Duplicate)) throw new InvalidOperationException(ScheduleSplitStatusCode.SCHEDULE_DUPLICATION_UNSUPPORTED + ": schedule cannot be duplicated.");
                        using (var duplicateTransaction = new Transaction(doc, "Duplicate schedule for split"))
                        {
                            KhimTools.Core.Revit.TransactionBoundary.Start(duplicateTransaction, "ScheduleSplit working-copy duplication");
                            ElementId id = source.Duplicate(ViewDuplicateOption.Duplicate);
                            working = doc.GetElement(id) as ViewSchedule;
                            if (working == null) throw new InvalidOperationException("Working copy could not be resolved.");
                            working.Name = UniqueName(doc, source.Name + " - Split");
                            KhimTools.Core.Revit.TransactionBoundary.Commit(duplicateTransaction, "ScheduleSplit working-copy duplication");
                        }
                        plan.WorkingScheduleId = working.Id;
                        result.WorkingScheduleId = working.Id;
                    }
                    using (var splitTransaction = new Transaction(doc, "Split schedule segments"))
                    {
                        KhimTools.Core.Revit.TransactionBoundary.Start(splitTransaction, "ScheduleSplit schedule split");
                        if (!ScheduleSplitApiAdapter.IsSplit(working)) ScheduleSplitApiAdapter.Split(working, plan.SegmentHeightsInternal);
                        else if (!options.ReLayoutExistingSegments) throw new InvalidOperationException(ScheduleSplitStatusCode.ALREADY_SPLIT + ": existing split requires relayout mode.");
                        KhimTools.Core.Revit.TransactionBoundary.Commit(splitTransaction, "ScheduleSplit schedule split");
                    }
                    using (var placeTransaction = new Transaction(doc, "Place schedule segments"))
                    {
                        KhimTools.Core.Revit.TransactionBoundary.Start(placeTransaction, "ScheduleSplit sheet placement");
                        foreach (ScheduleSegmentPlan segment in plan.Segments.OrderBy(s => s.SegmentIndex))
                        {
                            ScheduleSheetInstance instance = null;
                            foreach (ScheduleSheetInstance candidate in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
                            {
                                if (candidate.ScheduleId == working.Id && candidate.OwnerViewId == segment.TargetSheetId && ScheduleSplitApiAdapter.GetSegmentIndex(candidate) == segment.SegmentIndex) { instance = candidate; break; }
                            }
                            if (options.SourceMode == ScheduleSplitSourceMode.MODIFY_SOURCE_SCHEDULE && segment.SegmentIndex == 0 && plan.SourceSheetId == segment.TargetSheetId)
                            {
                                instance = doc.GetElement(plan.SourceInstanceId) as ScheduleSheetInstance;
                                if (instance != null)
                                {
                                    instance.Point = segment.PlannedPosition;
                                    instance.SegmentIndex = segment.SegmentIndex;
                                }
                            }
                            if (instance == null) instance = ScheduleSegmentPlacementService.PlaceSegment(doc, segment.TargetSheetId, working.Id, segment.PlannedPosition, segment.SegmentIndex);
                            segment.CreatedInstanceId = instance.Id; segment.ActualPosition = ScheduleSplitApiAdapter.GetPosition(instance);
                            result.CreatedInstanceIds.Add(instance.Id);
                            result.SegmentResults.Add(new ScheduleSegmentResult { SegmentIndex = segment.SegmentIndex, TargetSheetId = segment.TargetSheetId, TargetSheetNumber = segment.TargetSheetNumber, InstanceId = instance.Id, PlannedPosition = segment.PlannedPosition, ActualPosition = segment.ActualPosition, Status = ScheduleSplitStatusCode.CREATED, Message = "Placed" });
                        }
                        KhimTools.Core.Revit.TransactionBoundary.Commit(placeTransaction, "ScheduleSplit sheet placement");
                    }
                    result.SegmentCount = result.SegmentResults.Count;
                    if (!ScheduleSplitVerificationService.Verify(doc, plan, result, out string verifyMessage)) throw new InvalidOperationException(verifyMessage);
                    KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ScheduleSplit batch");
                    result.Status = ScheduleSplitStatusCode.CREATED;
                }
                catch (Exception ex)
                {
                    KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ScheduleSplit batch");
                    result.Status = Classify(ex);
                    result.Messages.Add(ex.Message);
                }
            }
            timer.Stop(); result.Duration = timer.Elapsed;
            Debug.WriteLine("[K-TOOLS][ScheduleSplit] source=" + result.SourceScheduleId + ", working=" + result.WorkingScheduleId + ", status=" + result.Status + ", segments=" + result.SegmentResults.Count + ", duration=" + result.Duration);
            return result;
        }

        private static string UniqueName(Document doc, string seed)
        {
            string baseName = string.IsNullOrWhiteSpace(seed) ? "Schedule - Split" : seed.Trim();
            string candidate = baseName; int suffix = 2;
            var names = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (names.Contains(candidate)) candidate = baseName + " " + suffix++;
            return candidate;
        }

        private static ScheduleSplitStatusCode Classify(Exception ex)
        {
            string text = ex == null ? string.Empty : ex.Message;
            foreach (ScheduleSplitStatusCode code in Enum.GetValues(typeof(ScheduleSplitStatusCode))) if (text.IndexOf(code.ToString(), StringComparison.OrdinalIgnoreCase) >= 0) return code;
            return ScheduleSplitStatusCode.FAILED;
        }
    }
}
