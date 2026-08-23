using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tạo (hoặc cập nhật) 1 View3D (isometric) riêng cho từng cột — đúng loại "3D View" của
    /// Revit (khác với ColumnRebarSectionViewGenerator là Plan View cắt ngang). Dùng SectionBox
    /// để crop/zoom sát quanh cột, ép Rebar hiện solid + không bị che bởi bê tông trong đúng
    /// view đó, để mở lên là thấy ngay thép 3D thật bao quanh cột.
    ///
    /// CẦN VERIFY khi build (không có Revit để test trong môi trường này):
    /// 1. View3D.CreateIsometric(doc, vft.Id) — chữ ký chuẩn nhưng property SectionBox có thể
    ///    cần set thêm Transform đúng hướng nếu cột bị xoay (hiện dùng transform mặc định,
    ///    đủ dùng khi cột không xoay).
    /// 2. Rebar.SetSolidInView / SetUnobscuredInView — cần đúng tên method của bản Revit bạn dùng.
    /// </summary>
    public class ColumnRebar3DViewGenerator
    {
        private readonly Document _doc;
        private const string ViewNamePrefix = "Rebar 3D - ";

        public ColumnRebar3DViewGenerator(Document doc) => _doc = doc;

        public View3D CreateOrUpdate(FamilyInstance column, List<Rebar> rebars, double marginMm = 300)
        {
            string mark = column.LookupParameter("Mark")?.AsString() ?? column.Id.ToLongValue().ToString();
            string viewName = ViewNamePrefix + mark;

            View3D view = FindExistingView(viewName) ?? CreateView(viewName);

            ConfigureSectionBox(view, column, marginMm);

            try { view.DetailLevel = ViewDetailLevel.Fine; } catch { /* ignore */ }

            foreach (var rebar in rebars)
                TryMakeVisibleAndSolid(rebar, view);

            return view;
        }

        private View3D FindExistingView(string name) =>
            new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name == name);

        private View3D CreateView(string name)
        {
            ViewFamilyType vft = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.ThreeDimensional);

            if (vft == null)
                throw new InvalidOperationException("Project không có ViewFamilyType dạng 3D View.");

            View3D view = View3D.CreateIsometric(_doc, vft.Id);

            try { view.Name = name; }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                view.Name = name + " (" + DateTime.Now.ToString("HHmmss") + ")";
            }
            return view;
        }

        /// <summary>Crop/zoom view vào đúng vùng bao quanh cột (+ margin) bằng SectionBox.</summary>
        private void ConfigureSectionBox(View3D view, FamilyInstance column, double marginMm)
        {
            try
            {
                BoundingBoxXYZ colBb = column.get_BoundingBox(null);
                if (colBb == null) return;

                double margin = ToFeet(marginMm);

                BoundingBoxXYZ box = new BoundingBoxXYZ
                {
                    Min = new XYZ(colBb.Min.X - margin, colBb.Min.Y - margin, colBb.Min.Z - margin),
                    Max = new XYZ(colBb.Max.X + margin, colBb.Max.Y + margin, colBb.Max.Z + margin)
                };

                view.IsSectionBoxActive = true;
                view.SetSectionBox(box);
            }
            catch
            {
                // Không chặn cả lệnh nếu section box lỗi — view vẫn tạo được, chỉ chưa zoom sát cột
                // (người dùng tự Zoom to Fit trong Revit).
            }
        }

        private void TryMakeVisibleAndSolid(Rebar rebar, View view)
        {
            try
            {
                var method = rebar.GetType().GetMethod("SetSolidInView");
                if (method != null && view is View3D view3D)
                {
                    method.Invoke(rebar, new object[] { view3D, true });
                }
            }
            catch { }

            try
            {
                var method = rebar.GetType().GetMethod("SetUnobscuredInView");
                if (method != null)
                {
                    method.Invoke(rebar, new object[] { view, true });
                }
            }
            catch { }
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
