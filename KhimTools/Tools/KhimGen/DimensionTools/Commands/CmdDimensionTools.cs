using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.DimensionTools.Core;
using KhimTools.DimensionTools.Forms;
using KhimTools.DimensionTools.Models;
using KhimTools.DimensionTools.Services;
using KhimTools.Core.Workflow;
using KhimTools.Core.Logging;

namespace KhimTools.DimensionTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class CmdDimensionTools : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData == null || commandData.Application == null ? null : commandData.Application.ActiveUIDocument; if (uidoc == null || uidoc.Document == null) return Result.Cancelled; View view = uidoc.ActiveView; IList<ElementId> selection = uidoc.Selection.GetElementIds().ToList(); if (!ViewPlane.IsSupportedView(view)) { TaskDialog.Show("Dimension Tools", "UNSUPPORTED_VIEW_TYPE"); return Result.Cancelled; }
            using (var form = new DimensionToolsForm(uidoc, view, selection)) { if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || form.Plan == null) return Result.Cancelled; DimensionResult result = ExecutePlan(uidoc.Document, form.Plan); TaskDialog.Show("Dimension Tools 2.0", result.Status + Environment.NewLine + (result.Message ?? string.Empty)); }
            return Result.Succeeded;
        }
        private static DimensionResult ExecutePlan(Document doc, DimensionPlan plan)
        {
            Stopwatch timer = Stopwatch.StartNew();
            DimensionResult result = ExecutePlanCore(doc, plan);
            timer.Stop(); result.Duration = timer.Elapsed;
            bool success = result.Outcome == WorkflowOutcome.Succeeded || result.Outcome == WorkflowOutcome.Partial;
            string documentKey = doc == null ? string.Empty : DocumentIdentity.From(doc).StableKey;
            int affected = result.CreatedDimensionIds.Count + result.DeletedDimensionIds.Count + (result.Status == DimensionStatus.UPDATED ? result.SourceDimensionIds.Count : 0);
            WorkflowDiagnostic diagnostic = success
                ? WorkflowDiagnostic.Info("DIMENSION_WORKFLOW_COMPLETED", result.Message ?? result.Status.ToString(), "document=" + documentKey + ";operation=" + result.Operation + ";requested=" + result.SourceElementIds.Count + ";affected=" + affected + ";durationMs=" + timer.Elapsed.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                : WorkflowDiagnostic.Error("DIMENSION_WORKFLOW_FAILED", result.Message ?? result.Status.ToString(), false, context: "document=" + documentKey + ";operation=" + result.Operation + ";requested=" + result.SourceElementIds.Count + ";affected=" + affected + ";durationMs=" + timer.Elapsed.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture));
            result.Diagnostics.Add(diagnostic);
            KToolsLog.Current.Log(success ? WorkflowSeverity.Info : WorkflowSeverity.Error, "CmdDimensionTools." + result.Operation, diagnostic.ToString(), result.Status.ToString());
            return result;
        }

        private static DimensionResult ExecutePlanCore(Document doc, DimensionPlan plan)
        {
            if (doc == null || plan == null || plan.Context == null || !string.Equals(plan.DocumentIdentityKey, KhimTools.Core.Workflow.DocumentIdentity.From(doc).StableKey, StringComparison.Ordinal)) return new DimensionResult { Operation = plan == null ? DimensionOperation.GENERAL : plan.Operation, Status = DimensionStatus.STALE_DIMENSION_PLAN, Message = "Plan belongs to another or unavailable document." };
            View view = doc.GetElement(plan.ViewId) as View;
            if (view == null || (!string.IsNullOrEmpty(plan.ViewUniqueId) && !string.Equals(view.UniqueId, plan.ViewUniqueId, StringComparison.Ordinal))) return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.STALE_DIMENSION_PLAN, Message = "Planned view is no longer available." };
            for (int i = 0; i < plan.Context.DimensionIds.Count; i++) { Element dimension = doc.GetElement(plan.Context.DimensionIds[i]); if (dimension == null || (i < plan.Context.DimensionUniqueIds.Count && !string.IsNullOrEmpty(plan.Context.DimensionUniqueIds[i]) && !string.Equals(dimension.UniqueId, plan.Context.DimensionUniqueIds[i], StringComparison.Ordinal))) return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.STALE_DIMENSION_PLAN, Message = "A selected dimension changed after Preview." }; }
            if (plan.Context.DimensionIds.Count > 0 && !string.Equals(plan.SourceStateFingerprint, DimensionPlanBuilder.ComputeEditSourceFingerprint(doc, plan.Context), StringComparison.Ordinal)) return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.STALE_DIMENSION_PLAN, Message = "Selected dimension state changed after Preview." };
            switch (plan.Operation)
            {
                case DimensionOperation.GRID: return GridDimensionService.Create(doc, plan);
                case DimensionOperation.COLUMN: return ColumnDimensionService.Create(doc, plan);
                case DimensionOperation.BEAM: return BeamDimensionService.Create(doc, plan);
                case DimensionOperation.FOUNDATION: return FoundationDimensionService.Create(doc, plan);
                case DimensionOperation.OPENING: return OpeningDimensionService.Create(doc, plan);
                case DimensionOperation.ELEVATION: return ElevationDimensionService.CreateLevelChain(doc, plan);
                case DimensionOperation.SPOT_ELEVATION: return CreateSpot(doc, plan);
                case DimensionOperation.QUICK: return QuickDimensionService.Create(doc, plan);
                case DimensionOperation.GENERAL: return GeneralDimensionService.Create(doc, plan);
                case DimensionOperation.CUT: return plan.Context.DimensionIds.Count == 1 ? CutDimensionService.Cut(doc, view, plan.Context.DimensionIds[0], plan.Context.Options.BoundaryIndex) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "CUT requires one selected Dimension." };
                case DimensionOperation.JOIN: return plan.Context.DimensionIds.Count == 2 ? JoinDimensionService.Join(doc, view, plan.Context.DimensionIds[0], plan.Context.DimensionIds[1]) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "JOIN requires two selected Dimensions." };
                case DimensionOperation.REMOVE_ZERO: return plan.Context.DimensionIds.Count == 1 ? ZeroDimensionService.RemoveZero(doc, view, plan.Context.DimensionIds[0], plan.Context.Options.ToleranceMillimeters) : new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "REMOVE_ZERO requires one selected Dimension." };
                case DimensionOperation.MOVE_TEXT: if (plan.Context.DimensionIds.Count != 1) return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "MOVE_TEXT requires one selected Dimension." }; return DimensionTextService.MoveText(doc, view, plan.Context.DimensionIds[0], plan.Context.Options.HorizontalMoveMillimeters, plan.Context.Options.VerticalMoveMillimeters);
                default: return new DimensionResult { Operation = plan.Operation, Status = DimensionStatus.INVALID_SELECTION, Message = "Select a supported operation and valid references." };
            }
        }
        private static DimensionResult CreateSpot(Document doc, DimensionPlan plan)
        {
            if (plan.References.Count != 1) return new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION, Status = DimensionStatus.REFERENCE_NOT_FOUND, Message = "Spot Elevation requires one valid face Reference." };
            DimensionReferenceSnapshot snapshot = plan.References[0]; Reference resolved = DimensionReferenceService.ResolveStableReference(doc, snapshot); if (resolved == null) return new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION, Status = DimensionStatus.STALE_REFERENCE, Message = "Spot face Reference is stale." }; string live = DimensionReferenceService.ComputeLiveGeometryFingerprint(doc, snapshot, resolved); if (!string.Equals(live, snapshot.GeometryFingerprint, StringComparison.Ordinal)) return new DimensionResult { Operation = DimensionOperation.SPOT_ELEVATION, Status = DimensionStatus.STALE_DIMENSION_PLAN, Message = "Spot source geometry changed after Preview." }; DimensionReferenceInfo info = DimensionReferenceService.ToTransientInfo(snapshot); DimensionReferenceService.RefreshResolvedGeometry(doc, doc.GetElement(plan.ViewId) as View, info, resolved); ViewPlane plane = ViewPlane.FromView(doc.GetElement(plan.ViewId) as View); XYZ target = info.WorldPoint ?? XYZ.Zero; double offset = DimensionGeometryService.Mm(Math.Abs(plan.Context.Options.OffsetMillimeters) < 0.1 ? 100 : plan.Context.Options.OffsetMillimeters); XYZ origin = target + plane.RightDirection * offset; XYZ bend = origin + plane.UpDirection * DimensionGeometryService.Mm(20); XYZ end = bend + plane.RightDirection * DimensionGeometryService.Mm(20); return ElevationDimensionService.CreateSpotElevation(doc, doc.GetElement(plan.ViewId) as View, resolved, origin, bend, end, target, ElementId.InvalidElementId);
        }
    }
}
