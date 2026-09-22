using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.DB;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class RevisionSnapshotService
    {
        public static List<RevisionSnapshot> GetHistory(Document doc)
        {
            return ExtensibleStorageService.LoadSnapshots(doc);
        }

        public static RevisionSnapshot GetLatestSnapshot(Document doc)
        {
            var snapshots = GetHistory(doc);
            return snapshots.OrderByDescending(s => s.ExportTime).FirstOrDefault();
        }

        public static void CompareAndUpdateStatus(Document doc, List<SheetExportItem> currentSheets)
        {
            var latestSnapshot = GetLatestSnapshot(doc);
            if (latestSnapshot == null || latestSnapshot.Items == null || !latestSnapshot.Items.Any())
            {
                foreach (var sheet in currentSheets)
                {
                    sheet.IssueStatus = SheetIssueStatus.New;
                }
                return;
            }

            var map = latestSnapshot.Items
                .GroupBy(i => i.SheetUniqueId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in currentSheets)
            {
                if (map.TryGetValue(sheet.SheetUniqueId, out var oldItem))
                {
                    string currentFingerprint = new SheetIssueFingerprint { SheetUniqueId = sheet.SheetUniqueId, SheetNumber = sheet.SheetNumber,
                        SheetName = sheet.SheetName, RevisionNumber = sheet.CurrentRevisionNumber, RevisionDate = sheet.CurrentRevisionDate }.Value;
                    string previousFingerprint = new SheetIssueFingerprint { SheetUniqueId = oldItem.SheetUniqueId, SheetNumber = oldItem.SheetNumber,
                        SheetName = oldItem.SheetName, RevisionNumber = oldItem.RevisionNumber, RevisionDate = oldItem.RevisionDate }.Value;
                    sheet.RevisionFingerprint = currentFingerprint;
                    if (!string.Equals(currentFingerprint, previousFingerprint, StringComparison.Ordinal))
                    {
                        sheet.IssueStatus = SheetIssueStatus.Modified;
                    }
                    else
                    {
                        sheet.IssueStatus = SheetIssueStatus.Unchanged;
                    }
                }
                else
                {
                    sheet.IssueStatus = SheetIssueStatus.New;
                }
            }
        }

        public static List<SheetExportItem> GetDeletedHistoricalItems(List<SheetExportItem> currentSheets, RevisionSnapshot snapshot)
        {
            var currentIds = new HashSet<string>((currentSheets ?? new List<SheetExportItem>()).Select(item => item.SheetUniqueId ?? ""), StringComparer.OrdinalIgnoreCase);
            return (snapshot?.Items ?? new List<SheetSnapshotItem>()).Where(item => !currentIds.Contains(item.SheetUniqueId))
                .GroupBy(item => item.SheetUniqueId, StringComparer.OrdinalIgnoreCase).Select(group =>
                {
                    SheetSnapshotItem oldItem = group.First();
                    return new SheetExportItem { SheetUniqueId = oldItem.SheetUniqueId, SheetNumber = oldItem.SheetNumber,
                        SheetName = oldItem.SheetName, CurrentRevisionNumber = oldItem.RevisionNumber, CurrentRevisionDate = oldItem.RevisionDate,
                        IssueStatus = SheetIssueStatus.Deleted, CanExport = false, IsSelected = false, ExportStatusText = "Đã xóa" };
                }).ToList();
        }

        public static bool CreateSnapshot(Document doc, string issueSetName, List<SheetExportItem> exportedSheets, string format)
        {
            var snapshots = GetHistory(doc);

            var newSnapshot = new RevisionSnapshot
            {
                ExportId = Guid.NewGuid().ToString(),
                ExportTime = DateTime.Now,
                ExportedBy = Environment.UserName,
                IssueSetName = string.IsNullOrWhiteSpace(issueSetName) ? "General Issue" : issueSetName.Trim(),
                Items = (exportedSheets ?? new List<SheetExportItem>()).Where(s => s != null && s.CanExport && !s.IsFailed).Select(s => new SheetSnapshotItem
                {
                    SheetUniqueId = s.SheetUniqueId,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.SheetName,
                    RevisionNumber = s.CurrentRevisionNumber,
                    RevisionDate = s.CurrentRevisionDate,
                    Format = format,
                    Fingerprint = new SheetIssueFingerprint { SheetUniqueId = s.SheetUniqueId, SheetNumber = s.SheetNumber,
                        SheetName = s.SheetName, RevisionNumber = s.CurrentRevisionNumber, RevisionDate = s.CurrentRevisionDate }.Value,
                    ExportFileName = s.ComputedFileName
                }).ToList()
            };

            snapshots.Add(newSnapshot);
            return ExtensibleStorageService.SaveSnapshots(doc, snapshots);
        }

        public static bool CreateSnapshot(Document doc, ExportJobOptions options, ExportBatchResult batch)
        {
            var successful = (batch?.Results ?? new List<ExportItemResult>()).Where(result => result.Success && File.Exists(result.FinalPath)).ToList();
            var snapshots = GetHistory(doc);
            var grouped = successful.GroupBy(result => result.Format).ToList();
            foreach (var group in grouped)
            {
                var items = new List<SheetExportItem>();
                foreach (ExportItemResult result in group)
                {
                    items.Add(new SheetExportItem { SheetUniqueId = result.SheetUniqueId, SheetNumber = result.SheetNumber,
                        ComputedFileName = Path.GetFileNameWithoutExtension(result.FinalPath), CurrentRevisionNumber = result.RevisionNumber,
                        CurrentRevisionDate = result.RevisionDate, CanExport = true });
                }
                CreateSnapshot(doc, options?.IssueSetName, items, group.Key.ToString());
            }
            return grouped.Count > 0;
        }
    }
}
