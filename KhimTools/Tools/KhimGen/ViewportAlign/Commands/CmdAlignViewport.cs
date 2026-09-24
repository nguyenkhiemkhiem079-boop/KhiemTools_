using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.ViewportAlign.Forms;
using KhimTools.ViewportAlign.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace KhimTools.ViewportAlign.Commands
{
    /// <summary>Command identity preserved for ribbon and workspace integrations.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignViewport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null)
            {
                TaskDialog.Show("Khim Tools", "Không tìm thấy tài liệu Revit đang mở.");
                return Result.Cancelled;
            }

            try
            {
                Viewport preSelectedVp = null;
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 1)
                    preSelectedVp = doc.GetElement(selectedIds.First()) as Viewport;

                var form = new AlignViewportForm(uidoc, preSelectedVp);
                if (form.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                AlignmentReference source = form.SourceReference;
                if (source == null && form.SourceViewport != null)
                    source = ViewportAlignService.CreateViewportReference(doc, form.SourceViewport);
                var targets = form.SelectedTargetViews ?? new List<TargetViewItem>();
                AlignmentOperation operation = form.SelectedOperation;
                if (source == null || targets.Count == 0)
                    return Result.Cancelled;

                // Re-resolve the reference after the modal form closes so stale element state cannot drive a transaction.
                source = source.ReferenceType == AlignmentReferenceType.VIEWPORT
                    ? ViewportAlignService.CreateViewportReference(doc, doc.GetElement(source.ElementId) as Viewport)
                    : ViewportAlignService.CreateScheduleReference(doc, doc.GetElement(source.ElementId) as ScheduleSheetInstance);
                if (source == null)
                {
                    TaskDialog.Show("Khim Tools", LanguageManager.IsEnglish
                        ? "The selected alignment reference no longer exists."
                        : "Đối tượng tham chiếu đã chọn không còn tồn tại.");
                    return Result.Cancelled;
                }

                var preflight = ViewportAlignmentPreflightService.Run(doc, source, targets, operation);
                var preflightById = preflight.ToDictionary(p => p.TargetId.IntegerValue, p => p);
                var readyTargets = targets.Where(t => t != null && preflightById.TryGetValue(t.ViewportOrScheduleId.IntegerValue, out var p) && p.CanExecute).ToList();
                if ((operation == AlignmentOperation.DISTRIBUTE_HORIZONTAL || operation == AlignmentOperation.DISTRIBUTE_VERTICAL) && readyTargets.Count < 3)
                {
                    TaskDialog.Show("Khim Tools", LanguageManager.IsEnglish
                        ? "Distribution requires at least three compatible targets."
                        : "Căn phân bố cần ít nhất ba đối tượng tương thích.");
                    return Result.Cancelled;
                }

                var distributionCenters = ViewportAlignService.ComputeDistributionCenters(doc, readyTargets, operation);
                var summary = new AlignmentBatchSummary
                {
                    Requested = targets.Count,
                    Ready = readyTargets.Count
                };
                var results = new List<AlignmentExecutionResult>();

                TransactionBoundary.ExecuteGroup(doc, "K-TOOLS Align Viewports 2.0", () =>
                {
                    foreach (var item in targets)
                    {
                        AlignmentPreflightResult preflightRow = item == null || item.ViewportOrScheduleId == null
                            ? null
                            : preflightById.TryGetValue(item.ViewportOrScheduleId.IntegerValue, out var row) ? row : null;
                        if (preflightRow == null || !preflightRow.CanExecute)
                        {
                            var blocked = new AlignmentExecutionResult
                            {
                                TargetElementId = item?.ViewportOrScheduleId ?? ElementId.InvalidElementId,
                                SheetNumber = item?.SheetNumber ?? "",
                                TargetName = item?.ViewName ?? "",
                                Operation = operation,
                                Status = AlignmentExecutionStatus.BLOCKED,
                                StatusCode = preflightRow?.StatusCode ?? AlignmentStatusCode.INVALID_TARGET,
                                Message = preflightRow?.Message ?? "Target is invalid."
                            };
                            results.Add(blocked);
                            summary.Add(blocked);
                            continue;
                        }

                        // Re-resolve all mutable state immediately before its transaction.
                        Element liveElement = doc.GetElement(item.ViewportOrScheduleId);
                        if (liveElement == null)
                        {
                            var missing = new AlignmentExecutionResult
                            {
                                TargetElementId = item.ViewportOrScheduleId,
                                SheetNumber = item.SheetNumber,
                                TargetName = item.ViewName,
                                Operation = operation,
                                Status = AlignmentExecutionStatus.BLOCKED,
                                StatusCode = AlignmentStatusCode.MISSING_ELEMENT,
                                Message = "Target no longer exists."
                            };
                            results.Add(missing);
                            summary.Add(missing);
                            continue;
                        }
                        item.IsPinned = liveElement.Pinned;
                        if (item.IsPinned)
                        {
                            var pinned = new AlignmentExecutionResult
                            {
                                TargetElementId = item.ViewportOrScheduleId,
                                SheetNumber = item.SheetNumber,
                                TargetName = item.ViewName,
                                Operation = operation,
                                Status = AlignmentExecutionStatus.BLOCKED,
                                StatusCode = AlignmentStatusCode.PINNED,
                                Message = "Pinned targets are not moved automatically."
                            };
                            results.Add(pinned);
                            summary.Add(pinned);
                            continue;
                        }
                        if (doc.IsReadOnly || item.IsReadOnly)
                        {
                            var readOnly = new AlignmentExecutionResult
                            {
                                TargetElementId = item.ViewportOrScheduleId,
                                SheetNumber = item.SheetNumber,
                                TargetName = item.ViewName,
                                Operation = operation,
                                Status = AlignmentExecutionStatus.BLOCKED,
                                StatusCode = AlignmentStatusCode.READ_ONLY,
                                Message = "Target or active document is read-only."
                            };
                            results.Add(readOnly);
                            summary.Add(readOnly);
                            continue;
                        }

                        try
                        {
                            AlignmentExecutionResult execution = TransactionBoundary.Execute(doc,
                                "Align " + (item.ViewName ?? "target"),
                                () =>
                                {
                                    distributionCenters.TryGetValue(item.ViewportOrScheduleId, out var distributionCenter);
                                    return ViewportAlignmentExecutor.Execute(doc, source, item, operation, distributionCenter);
                                },
                                shouldCommit: result => result != null &&
                                    result.Status != AlignmentExecutionStatus.FAILED &&
                                    result.Status != AlignmentExecutionStatus.BLOCKED);
                            results.Add(execution);
                            summary.Add(execution);
                        }
                        catch (Exception ex)
                        {
                            var execution = new AlignmentExecutionResult
                            {
                                TargetElementId = item.ViewportOrScheduleId,
                                SheetNumber = item.SheetNumber,
                                TargetName = item.ViewName,
                                Operation = operation,
                                Status = AlignmentExecutionStatus.FAILED,
                                StatusCode = AlignmentStatusCode.FAILED,
                                Message = ex.Message
                            };
                            results.Add(execution);
                            summary.Add(execution);
                        }
                    }
                    return true;
                });

                System.Diagnostics.Debug.WriteLine($"[ViewportAlign] source={source.ElementId.IntegerValue}; operation={operation}; requested={summary.Requested}; ready={summary.Ready}; changed={summary.Changed}; skipped={summary.Skipped}; failed={summary.Failed}");
                uidoc.RefreshActiveView();
                ShowSummary(summary, operation);
                return summary.Failed > 0 ? Result.Failed : Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools — Error", ex.Message);
                return Result.Failed;
            }
        }

        private static void ShowSummary(AlignmentBatchSummary summary, AlignmentOperation operation)
        {
            string text = LanguageManager.IsEnglish
                ? $"Alignment Completed\n\nRequested: {summary.Requested}\nReady: {summary.Ready}\nChanged: {summary.Changed}\nAlready aligned: {summary.AlreadyAligned}\nSkipped: {summary.Skipped}\nFailed: {summary.Failed}\nOperation: {operation}"
                : $"Đã hoàn tất căn chỉnh\n\nYêu cầu: {summary.Requested}\nSẵn sàng: {summary.Ready}\nĐã thay đổi: {summary.Changed}\nĐã đúng vị trí: {summary.AlreadyAligned}\nBỏ qua: {summary.Skipped}\nLỗi: {summary.Failed}\nThao tác: {operation}";
            TaskDialog.Show("Khim Tools — Align Viewports", text);
        }
    }
}
