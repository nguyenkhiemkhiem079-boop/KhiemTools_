using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// The outcome of attempting a join/unjoin geometry operation on a single
    /// candidate pair of floor elements.
    /// </summary>
    public sealed class JoinPairResult
    {
        /// <summary>
        /// ElementId of the first floor in the pair.
        /// </summary>
        public ElementId FloorIdA { get; }

        /// <summary>
        /// ElementId of the second floor in the pair.
        /// </summary>
        public ElementId FloorIdB { get; }

        /// <summary>
        /// True if the geometry operation was actually performed (state changed).
        /// False if it was skipped (e.g. already in the desired join state) or failed.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// True if an exception was thrown while attempting the operation.
        /// </summary>
        public bool IsError { get; }

        /// <summary>
        /// Additional context: "Joined", "Already joined", "Not joined", "Failed: <reason>", etc.
        /// </summary>
        public string Message { get; }

        public JoinPairResult(ElementId floorIdA, ElementId floorIdB, bool success, bool isError, string message)
        {
            FloorIdA = floorIdA;
            FloorIdB = floorIdB;
            Success = success;
            IsError = isError;
            Message = message;
        }
    }
}
