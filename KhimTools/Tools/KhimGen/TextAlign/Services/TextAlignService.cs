using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;

namespace KhimTools.TextAlign.Services
{
    public enum AlignType
    {
        Top,
        Bottom,
        Left,
        Right,
        Middle,
        HorizontalEquals,
        VerticalEquals
    }

    /// <summary>
    /// Service xử lý căn chỉnh và chia đều vị trí Text, Tag, Annotation và các đối tượng hình học trong View.
    /// </summary>
    public static class TextAlignService
    {
        public static void AlignSelectedElements(UIDocument uidoc, AlignType alignType)
        {
            if (uidoc == null) return;
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            var selIds = uidoc.Selection.GetElementIds().ToList();
            if (selIds.Count < 2)
            {
                try
                {
                    var refs = uidoc.Selection.PickObjects(ObjectType.Element,
                        LanguageManager.IsEnglish ? "Select elements to align (ESC to cancel)" : "Chọn các đối tượng cần căn chỉnh (Nhấn ESC để hủy)");
                    selIds = refs.Select(r => r.ElementId).Distinct().ToList();
                }
                catch
                {
                    return;
                }
            }

            if (selIds.Count < 2)
            {
                TaskDialog.Show("Align Elements",
                    LanguageManager.IsEnglish ? "Please select at least 2 elements to align." : "Vui lòng chọn ít nhất 2 đối tượng để thực hiện căn chỉnh.");
                return;
            }

            var items = new List<ElementAlignItem>();
            foreach (var id in selIds)
            {
                var elem = doc.GetElement(id);
                if (elem == null) continue;

                var bbox = elem.get_BoundingBox(view);
                if (bbox == null) continue;

                items.Add(new ElementAlignItem
                {
                    Element = elem,
                    MinX = bbox.Min.X,
                    MaxX = bbox.Max.X,
                    MinY = bbox.Min.Y,
                    MaxY = bbox.Max.Y,
                    CenterX = (bbox.Min.X + bbox.Max.X) * 0.5,
                    CenterY = (bbox.Min.Y + bbox.Max.Y) * 0.5
                });
            }

            if (items.Count < 2)
            {
                TaskDialog.Show("Align Elements",
                    LanguageManager.IsEnglish ? "Could not determine bounding box of selected elements in active view." : "Không thể xác định tọa độ của các đối tượng trong khung nhìn hiện hành.");
                return;
            }

            using (var tx = new Transaction(doc, $"K-TOOLS - Align {alignType}"))
            {
                tx.Start();

                switch (alignType)
                {
                    case AlignType.Left:
                    {
                        double targetX = items.Min(it => it.MinX);
                        foreach (var it in items)
                        {
                            double dx = targetX - it.MinX;
                            if (Math.Abs(dx) > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, it.Element.Id, new XYZ(dx, 0, 0));
                            }
                        }
                        break;
                    }

                    case AlignType.Right:
                    {
                        double targetX = items.Max(it => it.MaxX);
                        foreach (var it in items)
                        {
                            double dx = targetX - it.MaxX;
                            if (Math.Abs(dx) > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, it.Element.Id, new XYZ(dx, 0, 0));
                            }
                        }
                        break;
                    }

                    case AlignType.Top:
                    {
                        double targetY = items.Max(it => it.MaxY);
                        foreach (var it in items)
                        {
                            double dy = targetY - it.MaxY;
                            if (Math.Abs(dy) > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, it.Element.Id, new XYZ(0, dy, 0));
                            }
                        }
                        break;
                    }

                    case AlignType.Bottom:
                    {
                        double targetY = items.Min(it => it.MinY);
                        foreach (var it in items)
                        {
                            double dy = targetY - it.MinY;
                            if (Math.Abs(dy) > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, it.Element.Id, new XYZ(0, dy, 0));
                            }
                        }
                        break;
                    }

                    case AlignType.Middle:
                    {
                        double targetY = items.Average(it => it.CenterY);
                        foreach (var it in items)
                        {
                            double dy = targetY - it.CenterY;
                            if (Math.Abs(dy) > 0.0001)
                            {
                                ElementTransformUtils.MoveElement(doc, it.Element.Id, new XYZ(0, dy, 0));
                            }
                        }
                        break;
                    }

                    case AlignType.HorizontalEquals:
                    {
                        if (items.Count >= 3)
                        {
                            var sorted = items.OrderBy(it => it.CenterX).ToList();
                            double firstX = sorted.First().CenterX;
                            double lastX = sorted.Last().CenterX;
                            double span = lastX - firstX;
                            double step = span / (sorted.Count - 1);

                            for (int i = 1; i < sorted.Count - 1; i++)
                            {
                                double targetX = firstX + i * step;
                                double dx = targetX - sorted[i].CenterX;
                                if (Math.Abs(dx) > 0.0001)
                                {
                                    ElementTransformUtils.MoveElement(doc, sorted[i].Element.Id, new XYZ(dx, 0, 0));
                                }
                            }
                        }
                        break;
                    }

                    case AlignType.VerticalEquals:
                    {
                        if (items.Count >= 3)
                        {
                            var sorted = items.OrderByDescending(it => it.CenterY).ToList();
                            double firstY = sorted.First().CenterY;
                            double lastY = sorted.Last().CenterY;
                            double span = firstY - lastY;
                            double step = span / (sorted.Count - 1);

                            for (int i = 1; i < sorted.Count - 1; i++)
                            {
                                double targetY = firstY - i * step;
                                double dy = targetY - sorted[i].CenterY;
                                if (Math.Abs(dy) > 0.0001)
                                {
                                    ElementTransformUtils.MoveElement(doc, sorted[i].Element.Id, new XYZ(0, dy, 0));
                                }
                            }
                        }
                        break;
                    }
                }

                tx.Commit();
            }
        }

        private class ElementAlignItem
        {
            public Element Element { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
        }
    }
}