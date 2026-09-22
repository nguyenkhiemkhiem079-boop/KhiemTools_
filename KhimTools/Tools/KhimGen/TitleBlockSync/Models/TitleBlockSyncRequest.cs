using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.TitleBlockSync.Models
{
    public sealed class TitleBlockSyncRequest
    {
        public Document Document { get; set; }
        public ElementId SourceSheetId { get; set; } = ElementId.InvalidElementId;
        public ElementId SourceTitleBlockId { get; set; } = ElementId.InvalidElementId;
        public List<TitleBlockSyncTarget> Targets { get; } = new List<TitleBlockSyncTarget>();
        public List<ParameterKey> SelectedParameterKeys { get; } = new List<ParameterKey>();
        public TitleBlockSyncOptions Options { get; set; } = new TitleBlockSyncOptions();
    }
}
