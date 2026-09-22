using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using KhimTools.SheetCopy.Models;

namespace KhimTools.SheetCopy.Services
{
    public static class SheetCopyPlanner
    {
        public static SheetCopyPlan BuildPlan(Document doc, SheetCopyRequest request)
        {
            var plan = new SheetCopyPlan { Options = request == null || request.Options == null ? new SheetCopyOptions() : request.Options };
            var items = new List<SheetCopyItem>();
            if (request != null)
            {
                int index = 1;
                foreach (SheetCopyItem supplied in request.Items.Where(i => i != null && i.IsSelected))
                {
                    SheetCopyItem item = SheetCopyCollector.FindById(doc, supplied.SourceSheetId);
                    item.TargetSheetNumber = string.IsNullOrWhiteSpace(supplied.TargetSheetNumber) ? supplied.TargetSheetNumber : supplied.TargetSheetNumber.Trim();
                    item.TargetSheetNumber = string.IsNullOrWhiteSpace(item.TargetSheetNumber)
                        ? SheetCopyNamingService.ApplyPattern(plan.Options.NumberPattern, item, index, plan.Options.Prefix, plan.Options.Suffix)
                        : supplied.TargetSheetNumber.Trim();
                    item.TargetSheetName = string.IsNullOrWhiteSpace(supplied.TargetSheetName)
                        ? SheetCopyNamingService.ApplyPattern(plan.Options.NamePattern, item, index) : supplied.TargetSheetName.Trim();
                    item.ViewPolicy = plan.Options.ViewPolicy;
                    ApplyPolicy(item, plan.Options);
                    items.Add(item);
                    index++;
                }
            }
            plan.Items = items;
            plan.Fingerprint = ComputeFingerprint(items, plan.Options);
            return plan;
        }

        public static string ComputeFingerprint(IEnumerable<SheetCopyItem> items, SheetCopyOptions options)
        {
            var builder = new StringBuilder(options == null ? string.Empty : options.ViewPolicy.ToString());
            foreach (SheetCopyItem item in (items ?? Enumerable.Empty<SheetCopyItem>()).OrderBy(i => i.SourceSheetUniqueId, StringComparer.Ordinal))
            {
                builder.Append('|').Append(item.SourceSheetUniqueId).Append('|').Append(item.SourceSheetNumber)
                    .Append('|').Append(item.TargetSheetNumber).Append('|').Append(item.TargetSheetName);
                foreach (SheetCopyContentItem content in item.Contents.OrderBy(c => c.SourceUniqueId, StringComparer.Ordinal))
                    builder.Append('|').Append(content.SourceUniqueId).Append('|').Append(content.ContentKind).Append('|').Append(content.Action);
            }
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private static void ApplyPolicy(SheetCopyItem item, SheetCopyOptions options)
        {
            foreach (SheetCopyContentItem content in item.Contents)
            {
                if (content.ContentKind == SheetCopyContentKind.LEGEND_VIEWPORT)
                {
                    content.Action = options.ViewPolicy == ViewCopyPolicy.SHEET_ONLY ? SheetCopyAction.SKIP : (options.ReuseLegends ? SheetCopyAction.REUSE : SheetCopyAction.CREATE);
                }
                else if (content.ContentKind == SheetCopyContentKind.SCHEDULE)
                {
                    content.Action = options.ViewPolicy == ViewCopyPolicy.SHEET_ONLY ? SheetCopyAction.SKIP : (options.ReuseSchedules ? SheetCopyAction.REUSE : SheetCopyAction.SKIP);
                    if (content.IsSegmented) content.Status = SheetCopyStatusCode.SEGMENTED_SCHEDULE_DEFERRED.ToString();
                }
                else if (content.ContentKind == SheetCopyContentKind.REVISION_RELATED)
                {
                    content.Action = options.CopyRevisions ? SheetCopyAction.COPY : SheetCopyAction.SKIP;
                    content.Status = options.CopyRevisions ? string.Empty : SheetCopyStatusCode.REVISION_CONTENT_SKIPPED.ToString();
                }
                else if (content.ContentKind == SheetCopyContentKind.UNSUPPORTED)
                {
                    content.Action = SheetCopyAction.SKIP;
                    content.Status = SheetCopyStatusCode.UNSUPPORTED_SHEET_CONTENT.ToString();
                }
                else if (content.ContentKind == SheetCopyContentKind.SHEET_ANNOTATION)
                {
                    content.Action = options.CopySafeAnnotations ? SheetCopyAction.COPY : SheetCopyAction.SKIP;
                    if (!options.CopySafeAnnotations) content.Status = SheetCopyStatusCode.SKIPPED.ToString();
                }
                else if (content.ContentKind == SheetCopyContentKind.NORMAL_VIEWPORT)
                {
                    content.Action = options.ViewPolicy == ViewCopyPolicy.SHEET_ONLY ? SheetCopyAction.SKIP : SheetCopyAction.CREATE;
                }
            }
        }
    }
}
