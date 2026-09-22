using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SheetCopy.Models
{
    public sealed class SheetCopyRequest
    {
        public List<SheetCopyItem> Items { get; } = new List<SheetCopyItem>();
        public SheetCopyOptions Options { get; set; } = new SheetCopyOptions();
        public Document Document { get; set; }
        public bool IsPreview { get; set; } = true;
    }
}
