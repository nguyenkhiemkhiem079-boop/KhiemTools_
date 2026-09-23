using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Logging;
using KhimTools.Core.Workflow;

namespace KhimTools.ModifyObjects.Core
{
    /// <summary>Owns the command-level transaction group, consistency checks, and operation postconditions.</summary>
    public static class ModifyObjectExecutionService
    {
        public static ModifyObjectResult Execute(Document doc, ModifyObjectPlan plan, Func<ModifyObjectResult> operation)
        {
            Stopwatch timer = Stopwatch.StartNew();
            string operationName = plan == null || plan.Context == null ? "UNKNOWN" : plan.Context.Operation.ToString();
            ModelState before = Capture(doc, plan);
            var result = new ModifyObjectResult { PreviewOnly = plan == null || plan.Context == null || plan.Context.PreviewOnly, Operation = operationName, DocumentIdentityKey = plan == null ? string.Empty : plan.DocumentIdentityKey, FailureKind = "", ExceptionType = "" };
            if (doc == null || plan == null || plan.Context == null)
            {
                result.Status = ModifyObjectStatus.INVALID_SELECTION;
                result.FailureKind = "VALIDATION_FAILURE";
                result.FailureCount = 1;
                return Finish(result, timer, WorkflowDiagnostic.Error("MODIFY_OBJECTS_INVALID_REQUEST", "Document, plan, or plan context is unavailable."), false);
            }
            if (result.PreviewOnly)
            {
                result.Status = ModifyObjectStatus.PREVIEW_ONLY;
                result.TransactionResult = null;
                result.Postcondition = "Preview-only plan; execution boundary was not entered.";
                return Finish(result, timer, WorkflowDiagnostic.Info("MODIFY_OBJECTS_PREVIEW_ONLY", "No model writes were attempted."), false);
            }

            ModifyObjectPreflightResult preflight = ModifyObjectPreflightService.Validate(doc, plan);
            result.WarningCount = preflight.Warnings.Count;
            if (!preflight.IsValid)
            {
                result.Status = preflight.Statuses.Count == 0 ? ModifyObjectStatus.INVALID_SELECTION : preflight.Statuses[0];
                result.Message = string.Join("; ", preflight.Errors);
                result.FailureKind = "VALIDATION_FAILURE";
                result.FailureCount = 1;
                result.TransactionResult = null;
                result.RollbackVerified = ModelUnchanged(doc, before);
                result.Postcondition = "Preflight refusal must leave the complete element-ID set and source fingerprints unchanged.";
                result.PostconditionPassed = result.RollbackVerified;
                result.VerificationPassed = result.PostconditionPassed;
                return Finish(result, timer, WorkflowDiagnostic.Error("MODIFY_OBJECTS_VALIDATION", result.Message), false);
            }

            using (var group = new TransactionGroup(doc, "K-TOOLS Modify Objects - " + operationName))
            {
                try
                {
                    KhimTools.Core.Revit.TransactionBoundary.Start(group, "ModifyObjects." + operationName);
                    ModifyObjectResult operationResult = operation == null
                        ? new ModifyObjectResult { Status = ModifyObjectStatus.FAILED, Message = "No operation delegate." }
                        : operation();
                    if (operationResult == null) throw new InvalidOperationException("KTOOL_FAILURE: operation returned no result.");
                    Merge(result, operationResult);
                    result.Operation = operationName;
                    doc.Regenerate();

                    bool successStatus = IsMutationSuccess(result.Status);
                    if (successStatus)
                    {
                        string requirement;
                        string message;
                        result.PostconditionPassed = ModifyObjectPostconditionVerifier.Verify(doc, plan, result, out requirement, out message);
                        result.Postcondition = requirement + " Actual: " + message;
                        result.HostVerificationRequired = true;
                        result.VerificationPassed = result.PostconditionPassed;
                        if (!result.PostconditionPassed)
                        {
                            result.Status = ModifyObjectStatus.POST_VERIFY_FAILED;
                            result.Message = message;
                            result.FailureKind = "KTOOL_FAILURE";
                            result.FailureCount++;
                            KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects." + operationName);
                            RecordRollback(doc, group, before, result);
                            return Finish(result, timer, WorkflowDiagnostic.Error("MODIFY_OBJECTS_POSTCONDITION_FAILED", result.Postcondition), false);
                        }
                        string deltaMessage;
                        if (!VerifyElementDelta(doc, before, result, out deltaMessage))
                        {
                            result.PostconditionPassed = false;
                            result.VerificationPassed = false;
                            result.Postcondition += " Unexpected model delta: " + deltaMessage;
                            result.Status = ModifyObjectStatus.POST_VERIFY_FAILED;
                            result.FailureKind = "KTOOL_FAILURE";
                            result.FailureCount++;
                            KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects." + operationName);
                            RecordRollback(doc, group, before, result);
                            return Finish(result, timer, WorkflowDiagnostic.Error("MODIFY_OBJECTS_UNEXPECTED_DELTA", deltaMessage), false);
                        }

                        KhimTools.Core.Revit.TransactionBoundary.Assimilate(group, "ModifyObjects." + operationName);
                        result.TransactionResult = group.GetStatus();
                        if (result.TransactionResult != TransactionStatus.Committed)
                            throw new InvalidOperationException("TRANSACTION_COMMIT_FAILED: Modify Objects group status=" + result.TransactionResult);
                    }
                    else
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects." + operationName);
                        RecordRollback(doc, group, before, result);
                        if (result.Status == ModifyObjectStatus.FAILED || result.Status == ModifyObjectStatus.POST_VERIFY_FAILED)
                        { result.FailureKind = "KTOOL_FAILURE"; result.FailureCount = Math.Max(1, result.FailureCount); }
                    }
                }
                catch (Exception ex)
                {
                    result.ExceptionType = ex.GetType().FullName;
                    result.Message = ex.Message;
                    result.FailureKind = ex.Message != null && ex.Message.StartsWith("TRANSACTION_", StringComparison.Ordinal) ? "KTOOL_FAILURE" : IsRevitException(ex) ? "REVIT_FAILURE" : "KTOOL_FAILURE";
                    result.FailureCount++;
                    try
                    {
                        KhimTools.Core.Revit.TransactionBoundary.RollBack(group, "ModifyObjects." + operationName);
                        RecordRollback(doc, group, before, result);
                    }
                    catch (Exception rollbackException)
                    {
                        result.RollbackVerified = false;
                        result.RollbackResult = group.GetStatus();
                        result.Message += " | Rollback failure: " + rollbackException.Message;
                        result.FailureCount++;
                    }
                    result.Status = ModifyObjectStatus.FAILED;
                    KToolsLog.Current.Exception("ModifyObjects." + operationName, ex, result.FailureKind);
                }
            }
            result.AffectedElementCount = AffectedCount(result);
            WorkflowDiagnostic diagnostic = IsMutationSuccess(result.Status) && result.TransactionResult == TransactionStatus.Committed && result.PostconditionPassed
                ? WorkflowDiagnostic.Info("MODIFY_OBJECTS_SUCCESS", result.Postcondition, "affected=" + result.AffectedElementCount + ";tx=" + result.TransactionResult)
                : result.Status == ModifyObjectStatus.NO_CHANGE
                    ? WorkflowDiagnostic.Info("MODIFY_OBJECTS_NO_CHANGE", result.Message ?? "No model change was requested.", "tx=" + result.TransactionResult + ";rollback=" + result.RollbackVerified)
                    : WorkflowDiagnostic.Error("MODIFY_OBJECTS_FAILED", result.Message ?? result.Status.ToString(), false, null, "kind=" + result.FailureKind + ";tx=" + result.TransactionResult + ";rollback=" + result.RollbackVerified);
            return Finish(result, timer, diagnostic, true);
        }

        public static bool Verify(Document doc, ModifyObjectPlan plan)
        {
            if (doc == null || plan == null || plan.Context == null) return false;
            foreach (ModifyObjectSourceSnapshot source in plan.Sources)
                if (doc.GetElement(source.ElementId) == null && plan.Context.Operation != ModifyObjectOperation.COLUMN_SPLIT && plan.Context.Operation != ModifyObjectOperation.BEAM_SPLIT && plan.Context.Operation != ModifyObjectOperation.WALL_SPLIT && plan.Context.Operation != ModifyObjectOperation.COLUMN_JOIN && plan.Context.Operation != ModifyObjectOperation.BEAM_JOIN)
                    return false;
            return true;
        }

        private static void Merge(ModifyObjectResult target, ModifyObjectResult source)
        {
            target.Status = source.Status;
            target.Summary = source.Summary;
            target.Message = source.Message;
            target.CreatedElementIds.Clear(); foreach (ElementId id in source.CreatedElementIds) target.CreatedElementIds.Add(id);
            target.ModifiedElementIds.Clear(); foreach (ElementId id in source.ModifiedElementIds) target.ModifiedElementIds.Add(id);
            target.DeletedSourceIds.Clear(); foreach (ElementId id in source.DeletedSourceIds) target.DeletedSourceIds.Add(id);
            target.CreatedSupportElementIds.Clear(); foreach (ElementId id in source.CreatedSupportElementIds) target.CreatedSupportElementIds.Add(id);
            target.Diagnostics.Clear(); foreach (WorkflowDiagnostic diagnostic in source.Diagnostics) target.Diagnostics.Add(diagnostic);
        }

        private static bool IsMutationSuccess(ModifyObjectStatus status)
        { return status == ModifyObjectStatus.CREATED || status == ModifyObjectStatus.MODIFIED || status == ModifyObjectStatus.DELETED_SOURCE || status == ModifyObjectStatus.PARTIAL; }

        private static void RecordRollback(Document doc, TransactionGroup group, ModelState before, ModifyObjectResult result)
        {
            result.TransactionResult = group.GetStatus();
            result.RollbackResult = group.GetStatus();
            result.RollbackVerified = group.GetStatus() == TransactionStatus.RolledBack && ModelUnchanged(doc, before);
            if (!result.RollbackVerified) { result.FailureCount++; result.Message = (result.Message ?? string.Empty) + " | Rollback/model restoration could not be verified."; }
        }

        private static ModifyObjectResult Finish(ModifyObjectResult result, Stopwatch timer, WorkflowDiagnostic diagnostic, bool includeAffected)
        {
            timer.Stop();
            result.Duration = timer.Elapsed;
            if (includeAffected) result.AffectedElementCount = AffectedCount(result);
            if (diagnostic != null) result.Diagnostics.Add(diagnostic);
            TransactionStatus? txStatus = result.TransactionResult;
            WorkflowExecutionState executionState = result.FailureKind == "VALIDATION_FAILURE" ? WorkflowExecutionState.VALIDATION_FAILURE :
                result.FailureKind == "REVIT_FAILURE" ? WorkflowExecutionState.REVIT_FAILURE :
                result.FailureKind == "KTOOL_FAILURE" ? WorkflowExecutionState.KTOOL_FAILURE :
                result.TransactionResult == TransactionStatus.Committed && result.PostconditionPassed ? WorkflowExecutionState.SUCCESS :
                result.Status == ModifyObjectStatus.PREVIEW_ONLY ? WorkflowExecutionState.USER_CANCEL : WorkflowExecutionState.VALIDATION_FAILURE;
            var run = new WorkflowExecutionRecord
            {
                Command = "CmdModifyObjects", Operation = result.Operation ?? string.Empty,
                DocumentKey = result.DocumentIdentityKey ?? string.Empty,
                InputSummary = "operation=" + (result.Operation ?? string.Empty) + ";sourceCount=" + (result.CreatedElementIds.Count + result.ModifiedElementIds.Count + result.DeletedSourceIds.Count),
                State = executionState,
                TransactionState = txStatus == TransactionStatus.Committed ? WorkflowTransactionState.COMMITTED : txStatus == TransactionStatus.RolledBack ? WorkflowTransactionState.ROLLED_BACK : txStatus.HasValue ? WorkflowTransactionState.FAILED : WorkflowTransactionState.NOT_STARTED,
                RevitTransactionStatus = txStatus,
                PostconditionState = result.PostconditionPassed ? WorkflowPostconditionState.PASSED : result.Status == ModifyObjectStatus.PREVIEW_ONLY ? WorkflowPostconditionState.NOT_APPLICABLE : WorkflowPostconditionState.FAILED,
                Postcondition = result.Postcondition ?? string.Empty,
                AffectedElementCount = result.AffectedElementCount,
                WarningCount = result.WarningCount,
                FailureCount = result.FailureCount,
                Duration = result.Duration,
                ExceptionType = result.ExceptionType ?? string.Empty,
                RollbackResult = result.RollbackResult.HasValue ? result.RollbackResult.Value.ToString() : string.Empty,
                RollbackVerified = result.RollbackVerified,
                ModelUnchanged = result.RollbackVerified || (result.TransactionResult == null && (result.FailureKind == "VALIDATION_FAILURE" || result.Status == ModifyObjectStatus.PREVIEW_ONLY))
            };
            result.ExecutionDiagnostics = run;
            // Never relabel a committed model mutation as a failure: that would hide the
            // actual transaction state. A contradictory record is a programming defect.
            run.AssertConsistent(result.Outcome);
            if (diagnostic != null && diagnostic.Severity == WorkflowSeverity.Error)
                KToolsLog.Current.Log(WorkflowSeverity.Error, "ModifyObjects." + (result.Operation ?? "UNKNOWN"), RunSummary(run) + ";" + diagnostic.ToString(), diagnostic.Code);
            else if (diagnostic != null)
                KToolsLog.Current.Log(WorkflowSeverity.Info, "ModifyObjects." + (result.Operation ?? "UNKNOWN"), RunSummary(run) + ";" + diagnostic.ToString(), diagnostic.Code);
            return result;
        }

        private static string RunSummary(WorkflowExecutionRecord run)
        { return "state=" + run.State + ";tx=" + run.TransactionState + ";affected=" + run.AffectedElementCount + ";warnings=" + run.WarningCount + ";failures=" + run.FailureCount + ";durationMs=" + run.Duration.TotalMilliseconds.ToString("F0") + ";rollback=" + run.RollbackVerified; }

        private static int AffectedCount(ModifyObjectResult result)
        {
            // This is the persistent affected count, not attempted work. A rolled-back
            // group leaves zero persistent elements even if inner services returned IDs.
            if (result.TransactionResult == TransactionStatus.RolledBack) return 0;
            return result.CreatedElementIds.Concat(result.CreatedSupportElementIds).Concat(result.ModifiedElementIds).Concat(result.DeletedSourceIds).Where(id => id != null).Distinct().Count();
        }

        private static bool VerifyElementDelta(Document doc, ModelState before, ModifyObjectResult result, out string message)
        {
            HashSet<ElementId> after = new HashSet<ElementId>(new FilteredElementCollector(doc).ToElementIds());
            HashSet<ElementId> added = new HashSet<ElementId>(after.Except(before.ElementIds));
            HashSet<ElementId> deleted = new HashSet<ElementId>(before.ElementIds.Except(after));
            HashSet<ElementId> expectedAdded = new HashSet<ElementId>(result.CreatedElementIds.Concat(result.CreatedSupportElementIds).Where(id => id != null).Where(id => !before.ElementIds.Contains(id)));
            HashSet<ElementId> expectedDeleted = new HashSet<ElementId>(result.DeletedSourceIds.Where(id => id != null));
            bool valid = added.SetEquals(expectedAdded) && deleted.SetEquals(expectedDeleted);
            message = valid ? "Created/deleted element-ID delta matches operation output." : "added=" + string.Join(",", added.Except(expectedAdded)) + "; missingAdded=" + string.Join(",", expectedAdded.Except(added)) + "; deleted=" + string.Join(",", deleted.Except(expectedDeleted)) + "; missingDeleted=" + string.Join(",", expectedDeleted.Except(deleted));
            return valid;
        }

        private static bool IsRevitException(Exception exception)
        { return exception != null && exception.GetType().FullName != null && exception.GetType().FullName.StartsWith("Autodesk.Revit.Exceptions.", StringComparison.Ordinal); }

        private static ModelState Capture(Document doc, ModifyObjectPlan plan)
        {
            var state = new ModelState();
            if (doc == null) return state;
            foreach (ElementId id in new FilteredElementCollector(doc).ToElementIds()) state.ElementIds.Add(id);
            if (plan != null) foreach (ModifyObjectSourceSnapshot source in plan.Sources)
            {
                Element element = doc.GetElement(source.ElementId);
                if (element != null) state.Sources[source.ElementId] = ModifyObjectPlanBuilder.Capture(element);
            }
            return state;
        }

        private static bool ModelUnchanged(Document doc, ModelState before)
        {
            if (doc == null || before == null) return false;
            HashSet<ElementId> afterIds = new HashSet<ElementId>(new FilteredElementCollector(doc).ToElementIds());
            if (!before.ElementIds.SetEquals(afterIds)) return false;
            foreach (KeyValuePair<ElementId, ModifyObjectSourceSnapshot> pair in before.Sources)
            {
                Element element = doc.GetElement(pair.Key);
                if (element == null || ModifyObjectPlanBuilder.BuildFingerprint(element) != pair.Value.GeometryFingerprint || ModifyObjectPlanBuilder.Capture(element).ParameterFingerprint != pair.Value.ParameterFingerprint) return false;
            }
            return true;
        }

        private sealed class ModelState
        {
            public HashSet<ElementId> ElementIds { get; } = new HashSet<ElementId>();
            public Dictionary<ElementId, ModifyObjectSourceSnapshot> Sources { get; } = new Dictionary<ElementId, ModifyObjectSourceSnapshot>();
        }
    }
}
