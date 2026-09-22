using Autodesk.Revit.DB;

namespace KhimTools.ModifyObjects.Core
{
    /// <summary>Named service boundary kept parallel with the other K-TOOLS planners.</summary>
    public static class ModifyObjectPreflightService
    {
        public static ModifyObjectPreflightResult Validate(Document doc, ModifyObjectPlan plan)
        {
            return ModifyObjectPreflight.Validate(doc, plan);
        }
    }
}
