using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Models;

namespace KhimTools.SlabJoin.Interfaces
{
    /// <summary>
    /// Performs the actual JoinGeometryUtils operations against a set of
    /// candidate slab pairs, handling per-pair exceptions safely so that a
    /// single failure never aborts the whole batch.
    /// </summary>
    public interface ISlabJoinService
    {
        /// <summary>
        /// Joins geometry for every candidate pair that is not already joined.
        /// Must be called from within an active, started <see cref="Transaction"/>.
        /// </summary>
        IList<JoinPairResult> JoinSlabs(Document doc, IList<SlabPair> pairs);

        /// <summary>
        /// Unjoins geometry for every candidate pair that is currently joined.
        /// Must be called from within an active, started <see cref="Transaction"/>.
        /// </summary>
        IList<JoinPairResult> UnjoinSlabs(Document doc, IList<SlabPair> pairs);
    }
}
