using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.ElementTags.Services
{
    public sealed class TagHeightRange
    {
        public double Bottom { get; set; }
        public double Top { get; set; }

        public static TagHeightRange FromView(Document doc, ViewPlan view)
        {
            if (view.GenLevel == null) throw new InvalidOperationException("View không có tầng tham chiếu.");
            using (var range = view.GetViewRange())
            {
                return new TagHeightRange {
                    Bottom = Elevation(doc, view, range, PlanViewPlane.BottomClipPlane, false),
                    Top = Elevation(doc, view, range, PlanViewPlane.TopClipPlane, true)
                };
            }
        }

        private static double Elevation(Document doc, ViewPlan view, PlanViewRange range, PlanViewPlane plane, bool top)
        {
            var id = range.GetLevelId(plane);
            if (id == PlanViewRange.Unlimited) return top ? double.PositiveInfinity : double.NegativeInfinity;
            Level level = doc.GetElement(id) as Level;
            if (id == PlanViewRange.Current) level = view.GenLevel;
            if (id == PlanViewRange.LevelAbove || id == PlanViewRange.LevelBelow)
            {
                var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>();
                level = id == PlanViewRange.LevelAbove
                    ? levels.Where(l => l.Elevation > view.GenLevel.Elevation + 1e-6).OrderBy(l => l.Elevation).FirstOrDefault()
                    : levels.Where(l => l.Elevation < view.GenLevel.Elevation - 1e-6).OrderByDescending(l => l.Elevation).FirstOrDefault();
            }
            if (level == null) throw new InvalidOperationException("Không xác định được Level của View Range. Hãy dùng khoảng cao độ tùy chỉnh.");
            return level.Elevation + range.GetOffset(plane);
        }

        public bool Contains(Element host, View view)
        {
            const double tolerance = 1.0 / 304.8;
            if (host is Floor)
            {
                try { var z = TagPlacement.Anchor(host, view).Z; return z >= Bottom - tolerance && z <= Top + tolerance; }
                catch { return false; }
            }
            var box = host.get_BoundingBox(null);
            return box != null && box.Max.Z >= Bottom - tolerance && box.Min.Z <= Top + tolerance;
        }
    }
}
