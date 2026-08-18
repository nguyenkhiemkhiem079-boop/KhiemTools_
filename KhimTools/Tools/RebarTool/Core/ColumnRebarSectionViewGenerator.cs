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
    /// Generator tự động tạo View Plan / View Section ngang cắt qua thân Cột để kiểm tra thép.
    /// Thiết lập View Range, Bounding Box và Gán Visibility cho các Rebar được tạo.
    /// </summary>
    public class ColumnRebarSectionViewGenerator
    {
        private const string ViewNamePrefix = "KHIM_REBAR_SECTION_";
        private readonly Document _doc;

        public ColumnRebarSectionViewGenerator(Document doc) => _doc = doc;

        public ViewPlan CreateOrUpdate(FamilyInstance column, List<Rebar> rebars, double marginMm = 300)
        {
            string mark = column.LookupParameter("Mark")?.AsString() ?? column.Id.ToLongValue().ToString();
            string viewName = ViewNamePrefix + mark;

            ViewPlan view = FindExistingView(viewName) ?? CreateViewPlan(viewName, column);

            ConfigureViewRange(view, column);
            ConfigureCropBox(view, column, marginMm);

            try { view.DetailLevel = ViewDetailLevel.Fine; } catch { /* ignore */ }

            foreach (var rebar in rebars)
                TryMakeVisibleAndSolid(rebar, view);

            return view;
        }

        private ViewPlan FindExistingView(string name) =>
            new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.Name == name);

        private ViewPlan CreateViewPlan(string name, FamilyInstance column)
        {
            ViewFamilyType vft = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.StructuralPlan)
                ?? new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.FloorPlan);

            if (vft == null)
                throw new InvalidOperationException("Project không có ViewFamilyType Structural Plan hoặc Floor Plan.");

            ElementId levelId = column.LevelId;
            if (levelId == null || levelId == ElementId.InvalidElementId)
            {
                Parameter p = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)
                           ?? column.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM);
                if (p != null && p.HasValue) levelId = p.AsElementId();
            }

            if (levelId == null || levelId == ElementId.InvalidElementId)
            {
                levelId = new FilteredElementCollector(_doc).OfClass(typeof(Level)).FirstElementId();
            }

            if (levelId == null || levelId == ElementId.InvalidElementId)
                throw new InvalidOperationException("Không tìm thấy Level nào trong project để tạo ViewPlan.");

            ViewPlan view = ViewPlan.Create(_doc, vft.Id, levelId);

            try { view.Name = name; }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                view.Name = name + " (" + DateTime.Now.ToString("HHmmss") + ")";
            }
            return view;
        }

        /// <summary>Cắt ngang qua đúng giữa chiều cao cột (không phải giữa tầng như mặc định).</summary>
        private void ConfigureViewRange(ViewPlan view, FamilyInstance column)
        {
            try
            {
                BoundingBoxXYZ bb = column.get_BoundingBox(null);
                if (bb == null) return;

                double midZ = (bb.Min.Z + bb.Max.Z) / 2.0;
                Level level = _doc.GetElement(view.GenLevel.Id) as Level;
                double offsetFromLevel = midZ - (level?.Elevation ?? 0);

                PlanViewRange range = view.GetViewRange();
                range.SetOffset(PlanViewPlane.CutPlane, offsetFromLevel);
                range.SetOffset(PlanViewPlane.TopClipPlane, offsetFromLevel + ToFeet(150));
                range.SetOffset(PlanViewPlane.BottomClipPlane, offsetFromLevel - ToFeet(150));
                range.SetOffset(PlanViewPlane.ViewDepthPlane, offsetFromLevel - ToFeet(150));
                view.SetViewRange(range);
            }
            catch
            {
                // Giữ view range mặc định nếu API lệch chữ ký ở version Revit đang dùng —
                // view vẫn được tạo, chỉ có thể cắt không đúng ngay giữa cột.
            }
        }

        private void ConfigureCropBox(ViewPlan view, FamilyInstance column, double marginMm)
        {
            try
            {
                BoundingBoxXYZ bb = column.get_BoundingBox(null);
                if (bb == null) return;

                double margin = ToFeet(marginMm);
                double cx = (bb.Min.X + bb.Max.X) / 2.0;
                double cy = (bb.Min.Y + bb.Max.Y) / 2.0;
                double halfX = (bb.Max.X - bb.Min.X) / 2.0 + margin;
                double halfY = (bb.Max.Y - bb.Min.Y) / 2.0 + margin;

                view.CropBoxActive = true;
                view.CropBoxVisible = true;

                BoundingBoxXYZ crop = view.CropBox;
                crop.Min = new XYZ(cx - halfX, cy - halfY, crop.Min.Z);
                crop.Max = new XYZ(cx + halfX, cy + halfY, crop.Max.Z);
                view.CropBox = crop;
            }
            catch
            {
                // Không chặn cả lệnh nếu crop box lỗi — view vẫn tạo được, chỉ chưa crop sát cột.
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
