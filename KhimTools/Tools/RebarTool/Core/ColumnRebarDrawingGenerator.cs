using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    public enum ColumnShapeType { Circular, Rectangular }

    public class ColumnRebarDrawingInput
    {
        public string ColumnMark = "Column";
        public ColumnShapeType Shape = ColumnShapeType.Circular;

        // Cột tròn
        public double ColumnDiameterMm;
        public int MainBarQty;

        // Cột chữ nhật/vuông
        public double ColumnWidthMm;   // B
        public double ColumnHeightMm;  // H
        public int BarsAlongB = 3;
        public int BarsAlongH = 3;

        // Chung
        public string MainBarLabel = "?";     // VD "D20"
        public string StirrupLabel = "?";     // VD "D8"
        public double StirrupSpacingMm = 150;
        public double CoverMm = 25;
    }

    /// <summary>
    /// Tạo (hoặc cập nhật nếu đã tồn tại — theo tên view) 1 Drafting View mặt cắt cột (tròn
    /// hoặc chữ nhật/vuông): outline cột, vòng/đai, dấu vị trí thép chủ, dimension, ghi chú số
    /// liệu thép. Vẽ bằng DetailCurve/TextNote thuần 2D — không phụ thuộc view range hay
    /// visibility override của Rebar 3D, nên xuất bản vẽ ổn định.
    /// </summary>
    public class ColumnRebarDrawingGenerator
    {
        private readonly Document _doc;
        private const string ViewNamePrefix = "Rebar Detail - ";

        public ColumnRebarDrawingGenerator(Document doc) => _doc = doc;

        public ViewDrafting CreateOrUpdate(ColumnRebarDrawingInput input)
        {
            string viewName = ViewNamePrefix + input.ColumnMark;

            ViewDrafting view = FindExistingView(viewName);
            if (view != null)
                ClearViewContent(view);
            else
                view = CreateDraftingView(viewName);

            if (input.Shape == ColumnShapeType.Circular)
                DrawCircularColumn(view, input);
            else
                DrawRectangularColumn(view, input);

            return view;
        }

        private ViewDrafting FindExistingView(string name) =>
            new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .FirstOrDefault(v => v.Name == name);

        private ViewDrafting CreateDraftingView(string name)
        {
            ViewFamilyType vft = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting);

            if (vft == null)
                throw new InvalidOperationException("Project không có ViewFamilyType dạng Drafting (kiểm tra template).");

            ViewDrafting view = ViewDrafting.Create(_doc, vft.Id);
            try { view.Name = name; }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                view.Name = name + " (" + DateTime.Now.ToString("HHmmss") + ")";
            }
            return view;
        }

        private void ClearViewContent(ViewDrafting view)
        {
            var ids = new FilteredElementCollector(_doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElementIds();
            if (ids.Any()) _doc.Delete(ids);
        }

        // ===================== Cột tròn =====================

        private void DrawCircularColumn(ViewDrafting view, ColumnRebarDrawingInput input)
        {
            double mm = ToFeet(1);
            double r = input.ColumnDiameterMm / 2.0 * mm;
            XYZ center = XYZ.Zero;

            DrawCircle(view, center, r);
            double stirrupR = r - input.CoverMm * mm;
            DrawCircle(view, center, stirrupR);

            double mainR = stirrupR - 15 * mm;
            for (int i = 0; i < Math.Max(input.MainBarQty, 0); i++)
            {
                double angle = 2 * Math.PI * i / input.MainBarQty;
                XYZ p = new XYZ(mainR * Math.Cos(angle), mainR * Math.Sin(angle), 0);
                DrawCross(view, p, 15 * mm);
            }

            TryDrawLinearDimension(view, center - new XYZ(r, 0, 0), center + new XYZ(r, 0, 0), r * 0.2);

            string note = $"Main: {input.MainBarQty}{input.MainBarLabel}   " +
                           $"Stirrup: {input.StirrupLabel}@{input.StirrupSpacingMm:0}mm   " +
                           $"Cover: {input.CoverMm:0}mm";
            PlaceTextNote(view, new XYZ(-r, -r - 80 * mm, 0), note);
        }

        // ===================== Cột chữ nhật/vuông =====================

        private void DrawRectangularColumn(ViewDrafting view, ColumnRebarDrawingInput input)
        {
            double mm = ToFeet(1);
            double halfB = input.ColumnWidthMm / 2.0 * mm;
            double halfH = input.ColumnHeightMm / 2.0 * mm;
            XYZ center = XYZ.Zero;

            DrawRectangle(view, center, halfB, halfH);                              // outline cột

            double sB = halfB - input.CoverMm * mm;
            double sH = halfH - input.CoverMm * mm;
            DrawRectangle(view, center, sB, sH);                                    // đai

            double mB = sB - 15 * mm;
            double mH = sH - 15 * mm;
            foreach (var (x, y) in BuildPerimeterPoints(mB, mH, input.BarsAlongB, input.BarsAlongH))
                DrawCross(view, new XYZ(x, y, 0), 15 * mm);                          // dấu thép chủ

            TryDrawLinearDimension(view, new XYZ(-halfB, -halfH - 30 * mm, 0), new XYZ(halfB, -halfH - 30 * mm, 0), 0);
            TryDrawLinearDimension(view, new XYZ(-halfB - 30 * mm, -halfH, 0), new XYZ(-halfB - 30 * mm, halfH, 0), 0);

            string note = $"Main: {input.BarsAlongB}x{input.BarsAlongH} {input.MainBarLabel}   " +
                           $"Stirrup: {input.StirrupLabel}@{input.StirrupSpacingMm:0}mm   " +
                           $"Cover: {input.CoverMm:0}mm";
            PlaceTextNote(view, new XYZ(-halfB, -halfH - 100 * mm, 0), note);
        }

        /// <summary>Giống hệt logic BuildPerimeterPoints trong RectangularColumnRebarGenerator, để dấu vẽ khớp vị trí thép thật.</summary>
        private List<(double x, double y)> BuildPerimeterPoints(double halfB, double halfH, int barsB, int barsH)
        {
            barsB = Math.Max(barsB, 2);
            barsH = Math.Max(barsH, 2);
            var pts = new List<(double, double)>();

            for (int i = 0; i < barsB; i++)
            {
                double t = barsB == 1 ? 0 : (double)i / (barsB - 1);
                double x = -halfB + t * 2 * halfB;
                pts.Add((x, halfH));
                pts.Add((x, -halfH));
            }
            for (int i = 1; i < barsH - 1; i++)
            {
                double t = (double)i / (barsH - 1);
                double y = -halfH + t * 2 * halfH;
                pts.Add((halfB, y));
                pts.Add((-halfB, y));
            }
            return pts;
        }

        // ===================== Vẽ cơ bản =====================

        private void DrawCircle(ViewDrafting view, XYZ center, double radius)
        {
            Arc arc1 = Arc.Create(center, radius, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, radius, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
            _doc.Create.NewDetailCurve(view, arc1);
            _doc.Create.NewDetailCurve(view, arc2);
        }

        private void DrawRectangle(ViewDrafting view, XYZ center, double halfB, double halfH)
        {
            XYZ c1 = center + new XYZ(halfB, halfH, 0);
            XYZ c2 = center + new XYZ(halfB, -halfH, 0);
            XYZ c3 = center + new XYZ(-halfB, -halfH, 0);
            XYZ c4 = center + new XYZ(-halfB, halfH, 0);

            _doc.Create.NewDetailCurve(view, Line.CreateBound(c1, c2));
            _doc.Create.NewDetailCurve(view, Line.CreateBound(c2, c3));
            _doc.Create.NewDetailCurve(view, Line.CreateBound(c3, c4));
            _doc.Create.NewDetailCurve(view, Line.CreateBound(c4, c1));
        }

        private void DrawCross(ViewDrafting view, XYZ p, double size)
        {
            Line l1 = Line.CreateBound(p - new XYZ(size / 2, 0, 0), p + new XYZ(size / 2, 0, 0));
            Line l2 = Line.CreateBound(p - new XYZ(0, size / 2, 0), p + new XYZ(0, size / 2, 0));
            _doc.Create.NewDetailCurve(view, l1);
            _doc.Create.NewDetailCurve(view, l2);
        }

        /// <summary>
        /// CẦN VERIFY khi build: dimension 1 DetailLine bằng Reference tạo trực tiếp từ element
        /// (new Reference(detailLine)). Đã bọc try/catch nên nếu sai signature ở version Revit
        /// bạn dùng, bản vẽ vẫn có outline + ghi chú, chỉ thiếu mỗi dimension đó.
        /// offsetPerp: độ lệch vuông góc của đường dimension so với đường tham chiếu (để không đè lên outline); truyền 0 nếu muốn dimension nằm ngay cạnh.
        /// </summary>
        private void TryDrawLinearDimension(ViewDrafting view, XYZ p1, XYZ p2, double offsetPerp)
        {
            try
            {
                Line refLine = Line.CreateBound(p1, p2);
                DetailLine detailLine = _doc.Create.NewDetailCurve(view, refLine) as DetailLine;
                if (detailLine == null) return;

                ReferenceArray refs = new ReferenceArray();
                refs.Append(new Reference(detailLine));

                XYZ dir = (p2 - p1).Normalize();
                XYZ perp = new XYZ(-dir.Y, dir.X, 0) * offsetPerp;
                Line dimLine = Line.CreateBound(p1 + perp, p2 + perp);

                _doc.Create.NewDimension(view, dimLine, refs);
            }
            catch
            {
                // không chặn cả lệnh nếu dimension lỗi
            }
        }

        private void PlaceTextNote(ViewDrafting view, XYZ position, string text)
        {
            ElementId textTypeId = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .FirstElementId();

            if (textTypeId == null || textTypeId == ElementId.InvalidElementId) return;

            TextNote.Create(_doc, view.Id, position, text, textTypeId);
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
