using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Utilities
{
    /// <summary>
    /// Stateless, reusable predicate helpers that encode the business rules
    /// for which floor elements are eligible for automated join/unjoin
    /// processing. Kept independent of any specific service so they can be
    /// unit-tested and reused elsewhere (e.g. a future "Join Walls" tool).
    /// </summary>
    public static class ElementFilterUtils
    {
        /// <summary>
        /// True if the floor's "Structural" instance parameter is checked.
        /// </summary>
        public static bool IsStructural(Floor floor)
        {
            Parameter param = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
            return param != null && param.HasValue && param.AsInteger() != 0;
        }

        /// <summary>
        /// True if the element has been marked demolished in a phase
        /// (i.e. it has a non-null/valid DemolishedPhaseId).
        /// </summary>
        public static bool IsDemolished(Element element)
        {
            ElementId demolishedPhaseId = element.DemolishedPhaseId;
            return demolishedPhaseId != null && demolishedPhaseId != ElementId.InvalidElementId;
        }

        /// <summary>
        /// True if the element is currently a member of a Group.
        /// </summary>
        public static bool IsInGroup(Element element)
        {
            return element.GroupId != null && element.GroupId != ElementId.InvalidElementId;
        }

        /// <summary>
        /// True if the element belongs to a non-primary Design Option.
        /// Elements not associated with any Design Option (the main model)
        /// are always considered eligible.
        /// </summary>
        public static bool IsInNonPrimaryDesignOption(Element element)
        {
            DesignOption option = element.DesignOption;
            return option != null && !option.IsPrimary;
        }
    }
}
