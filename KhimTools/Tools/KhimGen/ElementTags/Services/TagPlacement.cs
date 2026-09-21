using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ElementTags.Services
{
    internal static class TagPlacement
    {
        internal const double MaxPaperSearchMm = 12;
        internal static double PaperDistance(View view, double mm) => mm / 304.8 * Math.Max(1, view.Scale);

        internal static Dictionary<ElementId, XYZ> AlignedAnchors(IEnumerable<Element> hosts, View view)
        {
            var anchors = new List<Tuple<ElementId, XYZ>>();
            foreach (var host in hosts)
                try { anchors.Add(Tuple.Create(host.Id, Anchor(host, view))); } catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] recoverable operation failed: " + ex); }
            var result = new Dictionary<ElementId, XYZ>();
            var pending = anchors.OrderBy(a => a.Item2.DotProduct(view.UpDirection)).ToList();
            while (pending.Count > 0)
            {
                double first = pending[0].Item2.DotProduct(view.UpDirection);
                var row = pending.TakeWhile(a => a.Item2.DotProduct(view.UpDirection) - first <= PaperDistance(view, 4)).ToList();
                double elevation = row.Average(a => a.Item2.DotProduct(view.UpDirection));
                foreach (var a in row)
                    result[a.Item1] = a.Item2 + view.UpDirection * (elevation - a.Item2.DotProduct(view.UpDirection));
                pending.RemoveRange(0, row.Count);
            }
            return result;
        }

        internal static XYZ Anchor(Element host, View view)
        {
            var box = host.get_BoundingBox(view);
            XYZ center = box == null ? null : (box.Min + box.Max) * 0.5;
            if (host is Floor floor && center != null)
            {
                // Project candidates onto trimmed top faces: IsInside excludes openings.
                var faces = HostObjectUtils.GetTopFaces(floor)
                    .Select(r => floor.GetGeometryObjectFromReference(r) as Face)
                    .Where(f => f != null).ToList();
                var candidates = new List<XYZ> { center };
                for (int x = 1; x < 20; x++)
                    for (int y = 1; y < 20; y++)
                        candidates.Add(new XYZ(box.Min.X + (box.Max.X - box.Min.X) * x / 20.0,
                            box.Min.Y + (box.Max.Y - box.Min.Y) * y / 20.0, box.Max.Z));
                foreach (var point in candidates.OrderBy(p => p.DistanceTo(center)))
                    foreach (var face in faces)
                    {
                        var projection = face.Project(point);
                        if (projection != null && face.IsInside(projection.UVPoint)) return projection.XYZPoint;
                    }
                throw new InvalidOperationException("Không tìm được điểm tag nằm trong biên sàn.");
            }
            if (host.Location is LocationPoint lp) return lp.Point;
            if (host.Location is LocationCurve lc) return lc.Curve.Evaluate(0.5, true);
            return center ?? throw new InvalidOperationException("Không xác định được vị trí host.");
        }

        private static double[] HeadBounds(Document doc, View view, IndependentTag tag)
        {
            bool leader = tag.HasLeader && !tag.Pinned;
            try
            {
                if (leader) tag.HasLeader = false;
                doc.Regenerate();
                var box = tag.get_BoundingBox(view);
                if (box == null) return null;
                var points = new List<XYZ>();
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                    points.Add(box.Transform.OfPoint(new XYZ(x == 0 ? box.Min.X : box.Max.X,
                        y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z)));
                return new[] { points.Min(p => p.DotProduct(view.RightDirection)),
                    points.Min(p => p.DotProduct(view.UpDirection)),
                    points.Max(p => p.DotProduct(view.RightDirection)),
                    points.Max(p => p.DotProduct(view.UpDirection)) };
            }
            finally { if (leader) tag.HasLeader = true; }
        }

        internal static List<double[]> Obstacles(Document doc, View view, IEnumerable<IndependentTag> tags)
        {
            var result = new List<double[]>();
            foreach (var tag in tags)
            {
                // A subtransaction keeps temporary leader changes out of existing annotations.
                using (var sub = new SubTransaction(doc))
                {
                    sub.Start();
                    try { var bounds = HeadBounds(doc, view, tag); if (bounds != null) result.Add(bounds); }
                    catch (Exception ex) { Debug.WriteLine("[K-TOOLS][ElementTags] recoverable operation failed: " + ex); }
                    finally { sub.RollBack(); }
                }
            }
            return result;
        }

        internal static bool Place(Document doc, View view, IndependentTag tag, XYZ origin, List<double[]> occupied, Element host = null, bool alignedRow = false)
        {
            var original = tag.TagHeadPosition;
            double gap = PaperDistance(view, 1), step = PaperDistance(view, 2);
            // Nearest first, bounded to 12 mm on paper; use the view's axes, not world Y.
            double maxSearch = PaperDistance(view, MaxPaperSearchMm);
            var offsets = new List<Tuple<int, int>>();
            for (int x = -6; x <= 6; x++) for (int y = -6; y <= 6; y++)
                if (x * x + y * y <= 36 && (!alignedRow || y == 0)) offsets.Add(Tuple.Create(x, y));
            foreach (var offset in offsets.OrderBy(o => o.Item1 * o.Item1 + o.Item2 * o.Item2)
                .ThenBy(o => o.Item2).ThenBy(o => o.Item1))
            {
                var candidate = origin + view.RightDirection * (offset.Item1 * step)
                    + view.UpDirection * (offset.Item2 * step);
                if (candidate.DistanceTo(origin) > maxSearch + 1e-9) continue;
                if (host is Floor floor)
                {
                    bool inside = HostObjectUtils.GetTopFaces(floor).Any(r =>
                    {
                        var face = floor.GetGeometryObjectFromReference(r) as Face;
                        var projected = face?.Project(candidate);
                        return projected != null && face.IsInside(projected.UVPoint);
                    });
                    if (!inside) continue;
                }
                tag.TagHeadPosition = candidate;
                var b = HeadBounds(doc, view, tag);
                if (b == null) break;
                if (occupied.Any(a => b[0] < a[2] + gap && b[2] > a[0] - gap
                    && b[1] < a[3] + gap && b[3] > a[1] - gap)) continue;
                occupied.Add(b);
                return true;
            }
            tag.TagHeadPosition = original;
            if (!alignedRow)
            {
                var fallback = HeadBounds(doc, view, tag);
                if (fallback != null) occupied.Add(fallback);
            }
            return false;
        }
    }
}
