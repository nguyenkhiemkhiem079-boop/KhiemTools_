using System;
using System.Collections.Generic;
using System.Linq;
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

            var map = latestSnapshot.Items.ToDictionary(i => i.SheetUniqueId, StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in currentSheets)
            {
                if (map.TryGetValue(sheet.SheetUniqueId, out var oldItem))
                {
                    if (!string.Equals(sheet.CurrentRevisionNumber, oldItem.RevisionNumber, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(sheet.SheetName, oldItem.SheetName, StringComparison.OrdinalIgnoreCase))
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

        public static bool CreateSnapshot(Document doc, string issueSetName, List<SheetExportItem> exportedSheets, string format)
        {
            var snapshots = GetHistory(doc);

            var newSnapshot = new RevisionSnapshot
            {
                ExportId = Guid.NewGuid().ToString(),
                ExportTime = DateTime.Now,
                ExportedBy = Environment.UserName,
                IssueSetName = string.IsNullOrWhiteSpace(issueSetName) ? "Official Release" : issueSetName.Trim(),
                Items = exportedSheets.Select(s => new SheetSnapshotItem
                {
                    SheetUniqueId = s.SheetUniqueId,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.SheetName,
                    RevisionNumber = s.CurrentRevisionNumber,
                    RevisionDate = s.CurrentRevisionDate,
                    Format = format,
                    ExportFileName = s.ComputedFileName
                }).ToList()
            };

            snapshots.Add(newSnapshot);
            return ExtensibleStorageService.SaveSnapshots(doc, snapshots);
        }
    }
}
