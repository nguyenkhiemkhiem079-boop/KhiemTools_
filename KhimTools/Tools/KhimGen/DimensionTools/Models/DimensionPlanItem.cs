using Autodesk.Revit.DB;

namespace KhimTools.DimensionTools.Models
{
    public sealed class DimensionPlanItem
    {
        public ElementId SourceElementId { get; set; }
        public DimensionChainPlan Chain { get; set; }
        public DimensionStatus Status { get; set; }
        public string Message { get; set; }
        public DimensionPlanItem() { Status = DimensionStatus.READY; }
    }
}
