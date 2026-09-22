using System;
using System.Collections.Generic;

namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyPlan
    {
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string Fingerprint { get; set; } = string.Empty;
        public string PlanVersion { get; set; } = "3.1";
        public SheetCopyOptions Options { get; set; } = new SheetCopyOptions();
        public IReadOnlyList<SheetCopyItem> Items { get; set; } = new List<SheetCopyItem>();
        public bool IsStale { get; set; }
        public bool HasBlockingItems
        {
            get
            {
                foreach (var item in Items)
                    if (item.Status != SheetCopyStatusCode.READY) return true;
                return false;
            }
        }
    }
}
