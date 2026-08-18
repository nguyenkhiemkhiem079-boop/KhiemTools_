using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Models;

namespace KhimTools.SlabJoin.Interfaces
{
    /// <summary>
    /// Discovers candidate pairs of floor elements that are spatially close
    /// enough to potentially intersect, without performing an O(n^2) brute-force
    /// comparison. Implementations should rely on Revit's native spatial
    /// filtering (BoundingBoxIntersectsFilter / Outline) so that each element's
    /// candidate set is narrowed down internally by Revit's spatial index.
    /// </summary>
    public interface ISpatialIndexService
    {
        /// <summary>
        /// Finds candidate pairs of floors whose (tolerance-expanded) bounding boxes intersect.
        /// Each unordered pair is returned exactly once.
        /// </summary>
        /// <param name="doc">The active document that owns the floors.</param>
        /// <param name="floors">The pre-filtered, eligible set of floors to pair up.</param>
        /// <returns>De-duplicated list of candidate slab pairs.</returns>
        IList<SlabPair> FindCandidatePairs(Document doc, IList<Floor> floors);
    }
}
