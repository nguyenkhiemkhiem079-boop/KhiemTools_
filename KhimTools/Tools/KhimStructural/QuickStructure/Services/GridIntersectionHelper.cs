using System;
using System.Collections.Generic;

namespace KhimTools.Structural.QuickStructure.Services
{
    /// <summary>
    /// Đối tượng điểm 2D phẳng phục vụ tính toán hình học độc lập với Revit API.
    /// </summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D() { }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2D other)
        {
            if (other == null) return double.MaxValue;
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override bool Equals(object obj)
        {
            var other = obj as Point2D;
            if (other == null) return false;
            return Math.Abs(X - other.X) < 1e-5 && Math.Abs(Y - other.Y) < 1e-5;
        }

        public override int GetHashCode()
        {
            return (int)(Math.Round(X, 4) * 10000 + Math.Round(Y, 4));
        }

        public override string ToString()
        {
            return string.Format("({0:0.##}, {1:0.##})", X, Y);
        }
    }

    /// <summary>
    /// Thuật toán hình học tính toán giao điểm lưới trục (Grid Intersections)
    /// và khử trùng lặp toạ độ trong không gian 2D/3D.
    /// </summary>
    public static class GridIntersectionHelper
    {
        /// <summary>
        /// Tìm giao điểm giữa 2 phân đoạn thẳng 2D (p1-p2 và p3-p4).
        /// Nếu extendInfinite = true thì xem 2 phân đoạn là đường thẳng kéo dài vô hạn.
        /// </summary>
        public static Point2D FindIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4, bool extendInfinite)
        {
            if (p1 == null || p2 == null || p3 == null || p4 == null) return null;

            double dx1 = p2.X - p1.X;
            double dy1 = p2.Y - p1.Y;
            double dx2 = p4.X - p3.X;
            double dy2 = p4.Y - p3.Y;

            double denom = dx1 * dy2 - dy1 * dx2;

            // Song song hoặc trùng phương
            if (Math.Abs(denom) < 1e-9)
            {
                return null;
            }

            double t = ((p3.X - p1.X) * dy2 - (p3.Y - p1.Y) * dx2) / denom;
            double u = ((p3.X - p1.X) * dy1 - (p3.Y - p1.Y) * dx1) / denom;

            if (!extendInfinite)
            {
                if (t < -1e-6 || t > 1.0 + 1e-6 || u < -1e-6 || u > 1.0 + 1e-6)
                {
                    return null;
                }
            }

            double ix = p1.X + t * dx1;
            double iy = p1.Y + t * dy1;

            return new Point2D(ix, iy);
        }

        /// <summary>
        /// Khử trùng lặp danh sách điểm giao cắt theo sai số dung sai (tolerance).
        /// </summary>
        public static List<Point2D> DeduplicatePoints(List<Point2D> points, double tolerance)
        {
            var result = new List<Point2D>();
            if (points == null || points.Count == 0) return result;

            if (tolerance <= 0) tolerance = 1e-5;

            foreach (var pt in points)
            {
                if (pt == null) continue;

                bool isDuplicate = false;
                foreach (var existing in result)
                {
                    if (pt.DistanceTo(existing) <= tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(pt);
                }
            }

            return result;
        }
    }
}
