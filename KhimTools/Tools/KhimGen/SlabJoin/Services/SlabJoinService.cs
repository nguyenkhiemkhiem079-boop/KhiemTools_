using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.SlabJoin.Interfaces;
using KhimTools.SlabJoin.Models;

namespace KhimTools.SlabJoin.Services
{
    /// <summary>
    /// Safe implementation of <see cref="ISlabJoinService"/>.
    ///
    /// Biện pháp chống văng Revit (Crash Prevention):
    ///   1. Bounding Box Touch Check (elements_might_touch): Chỉ xử lý các cặp sàn thực sự chạm nhau.
    ///   2. SubTransaction Isolation: Mỗi cặp sàn được thực hiện trong 1 SubTransaction riêng biệt.
    ///      Nếu có lỗi hình học nặng ở 1 cặp, SubTransaction sẽ RollBack cặp đó mà không ảnh hưởng
    ///      đến các cặp khác và không làm sập (crash) tiến trình Revit.
    ///   3. Thickness Priority: Sàn dày = Primary, sàn mỏng = Secondary.
    ///   4. Safe Unjoin-before-Join & Fallback Order.
    /// </summary>
    public sealed class SlabJoinService : ISlabJoinService
    {
        private const double BoundingBoxToleranceFeet = 0.003; // ~1mm tolerance

        public IList<JoinPairResult> JoinSlabs(Document doc, IList<SlabPair> pairs)
        {
            var results = new List<JoinPairResult>();
            if (pairs == null) return results;
            foreach (SlabPair pair in pairs)
                results.Add(TryJoinPair(doc, pair));
            return results;
        }

        public IList<JoinPairResult> UnjoinSlabs(Document doc, IList<SlabPair> pairs)
        {
            var results = new List<JoinPairResult>();
            if (pairs == null) return results;
            foreach (SlabPair pair in pairs)
                results.Add(TryUnjoinPair(doc, pair));
            return results;
        }

        // ─── JOIN ────────────────────────────────────────────────────────────

        private JoinPairResult TryJoinPair(Document doc, SlabPair pair)
        {
            Element elementA = doc.GetElement(pair.FloorIdA);
            Element elementB = doc.GetElement(pair.FloorIdB);

            if (elementA == null || elementB == null || !elementA.IsValidObject || !elementB.IsValidObject)
                return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, true,
                    "Failed: one or both elements are invalid or deleted.");

            // Kiểm tra Bounding Box chạm nhau trước khi can thiệp hình học
            if (!ElementsMightTouch(doc, elementA, elementB))
            {
                return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, false,
                    "Skipped: Bounding boxes do not touch.");
            }

            // Dùng SubTransaction cách ly từng cặp — chống văng Revit nếu 1 cặp bị lỗi hình học nặng
            using (var subTx = new SubTransaction(doc))
            {
                subTx.Start();
                try
                {
                    double thickA = GetThicknessFeet(doc, elementA as Floor);
                    double thickB = GetThicknessFeet(doc, elementB as Floor);

                    Element primary   = thickA >= thickB ? elementA : elementB;
                    Element secondary = thickA >= thickB ? elementB : elementA;

                    // Unjoin trước nếu đang joined
                    if (JoinGeometryUtils.AreElementsJoined(doc, primary, secondary))
                    {
                        JoinGeometryUtils.UnjoinGeometry(doc, primary, secondary);
                    }

                    // Thử join primary → secondary, nếu lỗi thử ngược
                    bool joined = TryJoinOrder(doc, primary, secondary)
                               || TryJoinOrder(doc, secondary, primary);

                    if (joined)
                    {
                        subTx.Commit();
                        return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, true, false, "Joined.");
                    }
                    else
                    {
                        subTx.RollBack();
                        return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, true,
                            "Failed: JoinGeometry rejected both orderings.");
                    }
                }
                catch (Exception ex)
                {
                    try { subTx.RollBack(); } catch { }
                    return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, true,
                        $"Failed: {ex.Message}");
                }
            }
        }

        private static bool TryJoinOrder(Document doc, Element a, Element b)
        {
            try
            {
                JoinGeometryUtils.JoinGeometry(doc, a, b);
                return true;
            }
            catch { return false; }
        }

        // ─── UNJOIN ──────────────────────────────────────────────────────────

        private JoinPairResult TryUnjoinPair(Document doc, SlabPair pair)
        {
            Element elementA = doc.GetElement(pair.FloorIdA);
            Element elementB = doc.GetElement(pair.FloorIdB);

            if (elementA == null || elementB == null || !elementA.IsValidObject || !elementB.IsValidObject)
                return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, true,
                    "Failed: one or both elements are invalid or deleted.");

            if (!ElementsMightTouch(doc, elementA, elementB))
            {
                return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, false,
                    "Not joined (boxes do not touch).");
            }

            using (var subTx = new SubTransaction(doc))
            {
                subTx.Start();
                try
                {
                    if (!JoinGeometryUtils.AreElementsJoined(doc, elementA, elementB))
                    {
                        subTx.RollBack();
                        return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, false, "Not joined.");
                    }

                    JoinGeometryUtils.UnjoinGeometry(doc, elementA, elementB);
                    subTx.Commit();
                    return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, true, false, "Unjoined.");
                }
                catch (Exception ex)
                {
                    try { subTx.RollBack(); } catch { }
                    return new JoinPairResult(pair.FloorIdA, pair.FloorIdB, false, true,
                        $"Failed: {ex.Message}");
                }
            }
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra BoundingBox 3D có chạm/giao nhau hay không (kèm tolerance)
        /// (Giống hàm elements_might_touch trong Python pyRevit reference).
        /// </summary>
        private static bool ElementsMightTouch(Document doc, Element el1, Element el2)
        {
            try
            {
                View activeView = doc.ActiveView;
                BoundingBoxXYZ bb1 = el1.get_BoundingBox(null) ?? el1.get_BoundingBox(activeView);
                BoundingBoxXYZ bb2 = el2.get_BoundingBox(null) ?? el2.get_BoundingBox(activeView);

                if (bb1 == null || bb2 == null) return false;

                double tol = 0.0328; // ~10 mm tolerance

                return (bb1.Max.X + tol >= bb2.Min.X && bb1.Min.X - tol <= bb2.Max.X) &&
                       (bb1.Max.Y + tol >= bb2.Min.Y && bb1.Min.Y - tol <= bb2.Max.Y) &&
                       (bb1.Max.Z + tol >= bb2.Min.Z && bb1.Min.Z - tol <= bb2.Max.Z);
            }
            catch
            {
                return false;
            }
        }

        private static double GetThicknessFeet(Document doc, Floor floor)
        {
            if (floor == null) return 0.5;
            try
            {
                FloorType ftype = doc.GetElement(floor.GetTypeId()) as FloorType;
                CompoundStructure cs = ftype?.GetCompoundStructure();
                if (cs != null && cs.GetWidth() > 0) return cs.GetWidth();

                double pThick = floor.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS)?.AsDouble()
                             ?? floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble()
                             ?? 0.5;
                return pThick;
            }
            catch { return 0.5; }
        }
    }
}
