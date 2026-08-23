using System;
using System.Collections.Generic;

namespace KhimTools.SheetExport.Models
{
    public class RevisionSnapshot
    {
        public string ExportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ExportTime { get; set; } = DateTime.Now;
        public string ExportedBy { get; set; } = Environment.UserName;
        public string IssueSetName { get; set; } = "General Issue";
        public List<SheetSnapshotItem> Items { get; set; } = new List<SheetSnapshotItem>();
    }

    public class SheetSnapshotItem
    {
        public string SheetUniqueId { get; set; } = "";
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public string RevisionNumber { get; set; } = "";
        public string RevisionDate { get; set; } = "";
        public string Format { get; set; } = "PDF";
        public string ExportFileName { get; set; } = "";
    }
}
