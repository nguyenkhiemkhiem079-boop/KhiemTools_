using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Structural.QuickStructure.Models;
using KhimTools.Structural.QuickStructure.Services;

namespace KhimTools.Structural.QuickStructure.Forms
{
    public partial class QuickStructureWindow : Window
    {
        private readonly Document _doc;
        private List<Grid> _grids = new List<Grid>();
        private List<XYZ> _intersections = new List<XYZ>();
        private List<Level> _levels = new List<Level>();

        private class ElementItem<T>
        {
            public T Item { get; set; }
            public string Name { get; set; }

            public ElementItem(T item, string name)
            {
                Item = item;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public QuickStructureWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // 1. Quét lưới trục và tính toán giao điểm
                _grids = QuickStructureService.GetAllGrids(_doc);
                _intersections = QuickStructureService.CalculateGridIntersections(_grids);
                int straightGridCount = _grids.Count(grid => grid.Curve is Line);
                int excludedGridCount = _grids.Count - straightGridCount;
                TxtStatus.Text = string.Format("{0} straight grids / {1} giao điểm{2}",
                    straightGridCount, _intersections.Count,
                    excludedGridCount == 0 ? string.Empty : string.Format("; {0} curved grids excluded", excludedGridCount));

                // 2. Thu thập danh sách Level
                _levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                var levelItems = _levels.Select(l => new ElementItem<Level>(l, l.Name)).ToList();

                CboColumnBaseLevel.ItemsSource = levelItems;
                CboColumnTopLevel.ItemsSource = levelItems;
                CboBeamLevel.ItemsSource = levelItems;
                CboFootingLevel.ItemsSource = levelItems;

                if (levelItems.Count > 0)
                {
                    CboColumnBaseLevel.SelectedIndex = 0;
                    CboColumnTopLevel.SelectedIndex = levelItems.Count > 1 ? 1 : 0;
                    CboBeamLevel.SelectedIndex = levelItems.Count > 1 ? 1 : 0;
                    CboFootingLevel.SelectedIndex = 0;
                }

                // 3. Thu thập Family Symbols: Cột kết cấu
                var columnSymbols = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .Select(s => new ElementItem<FamilySymbol>(s, s.Family.Name + " : " + s.Name))
                    .ToList();

                CboColumnType.ItemsSource = columnSymbols;
                if (columnSymbols.Count > 0) CboColumnType.SelectedIndex = 0;

                // 4. Thu thập Family Symbols: Dầm kết cấu
                var beamSymbols = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .Select(s => new ElementItem<FamilySymbol>(s, s.Family.Name + " : " + s.Name))
                    .ToList();

                CboBeamType.ItemsSource = beamSymbols;
                if (beamSymbols.Count > 0) CboBeamType.SelectedIndex = 0;

                // 5. Thu thập Family Symbols: Móng đơn
                var footingSymbols = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(s => s.Family.Name)
                    .ThenBy(s => s.Name)
                    .Select(s => new ElementItem<FamilySymbol>(s, s.Family.Name + " : " + s.Name))
                    .ToList();

                CboFootingType.ItemsSource = footingSymbols;
                if (footingSymbols.Count > 0) CboFootingType.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi nạp dữ liệu", ex.Message);
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            bool doCols = ChkCreateColumns.IsChecked == true;
            bool doBeams = ChkCreateBeams.IsChecked == true;
            bool doFootings = ChkCreateFootings.IsChecked == true;

            try
            {
                if (_intersections.Count == 0)
                    throw new InvalidOperationException("Không tìm thấy giao điểm lưới trục hợp lệ nào trong dự án.");
                if (!doCols && !doBeams && !doFootings)
                    throw new InvalidOperationException("Vui lòng chọn ít nhất một tác vụ mô hình hóa.");

                var column = CboColumnType.SelectedItem as ElementItem<FamilySymbol>;
                var columnBase = CboColumnBaseLevel.SelectedItem as ElementItem<Level>;
                var columnTop = CboColumnTopLevel.SelectedItem as ElementItem<Level>;
                var beam = CboBeamType.SelectedItem as ElementItem<FamilySymbol>;
                var beamLevel = CboBeamLevel.SelectedItem as ElementItem<Level>;
                var footing = CboFootingType.SelectedItem as ElementItem<FamilySymbol>;
                var footingLevel = CboFootingLevel.SelectedItem as ElementItem<Level>;

                if (doFootings && !doCols)
                    throw new InvalidOperationException("Tạo móng yêu cầu bật tạo cột trong cùng một lần chạy để bảo đảm batch nguyên tử.");
                if (doCols && (column == null || columnBase == null))
                    throw new InvalidOperationException("Vui lòng chọn Loại Cột và Tầng cơ sở (Base Level).");
                if (doBeams && (beam == null || beamLevel == null))
                    throw new InvalidOperationException("Vui lòng chọn Loại Dầm và Tầng Dầm.");
                if (doFootings && (footing == null || footingLevel == null))
                    throw new InvalidOperationException("Vui lòng chọn Loại Móng và Tầng Móng.");

                var request = new QuickStructureGenerationRequest
                {
                    Grids = _grids,
                    Intersections = _intersections,
                    CreateColumns = doCols,
                    ColumnSymbol = column?.Item,
                    ColumnBaseLevel = columnBase?.Item,
                    ColumnTopLevel = columnTop?.Item,
                    ColumnBaseOffsetMm = doCols ? ParseDouble(TxtColumnBaseOffset.Text) : 0,
                    ColumnTopOffsetMm = doCols ? ParseDouble(TxtColumnTopOffset.Text) : 0,
                    CreateBeams = doBeams,
                    BeamSymbol = beam?.Item,
                    BeamLevel = beamLevel?.Item,
                    BeamZOffsetMm = doBeams ? ParseDouble(TxtBeamZOffset.Text) : 0,
                    CreateFootings = doFootings,
                    FootingSymbol = footing?.Item,
                    FootingLevel = footingLevel?.Item,
                    FootingOffsetMm = doFootings ? ParseDouble(TxtFootingOffset.Text) : 0
                };

                QuickStructureGenerationResult result = QuickStructureService.Generate(_doc, request);
                int colsCount = result.Columns.Count;
                int beamsCount = result.Beams.Count;
                int footingsCount = result.Footings.Count;
                string msg = string.Format("Tạo thành công:\n• {0} Cột kết cấu\n• {1} Dầm kết cấu\n• {2} Móng đơn",
                    colsCount, beamsCount, footingsCount);

                Autodesk.Revit.UI.TaskDialog.Show("K-TOOLS — Quick Structure", msg);
                TxtStatus.Text = string.Format("Hoàn tất: {0} cột, {1} dầm, {2} móng.", colsCount, beamsCount, footingsCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][QuickStructure] Execution failed: " + ex);
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi thực thi", "Không thể tạo cấu kiện. " + ex.Message);
            }
        }

        private double ParseDouble(string text)
        {
            double val;
            string input = (text ?? "").Trim();
            if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
            {
                return val;
            }
            if (double.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
            {
                return val;
            }
            if (string.IsNullOrWhiteSpace(input)) return 0.0;
            throw new FormatException("Invalid numeric input: " + input);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
