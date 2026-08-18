using Autodesk.Revit.DB;
using KhimTools.Core;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Represents a candidate pair of structural floor elements that are spatially
    /// close enough to be considered for a join/unjoin geometry operation.
    /// </summary>
    public sealed class SlabPair
    {
        /// <summary>
        /// ElementId of the first floor in the pair.
        /// </summary>
        public ElementId FloorIdA { get; }

        /// <summary>
        /// ElementId of the second floor in the pair.
        /// </summary>
        public ElementId FloorIdB { get; }

        public SlabPair(ElementId floorIdA, ElementId floorIdB)
        {
            FloorIdA = floorIdA;
            FloorIdB = floorIdB;
        }

        /// <summary>
        /// Builds a canonical, order-independent string key for a pair of element ids.
        /// Used to de-duplicate candidate pairs discovered from both directions
        /// (A found near B, and B found near A) without O(n^2) bookkeeping.
        /// </summary>
        public static string MakeKey(ElementId idA, ElementId idB)
        {
            long a = idA.ToLongValue();
            long b = idB.ToLongValue();
            return a < b ? $"{a}_{b}" : $"{b}_{a}";
        }

        public override string ToString() => $"({FloorIdA.ToLongValue()} <-> {FloorIdB.ToLongValue()})";
    }
}
