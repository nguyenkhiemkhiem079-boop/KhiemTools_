using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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

                TxtGridStats.Text = string.Format("{0} Grids / {1} Giao điểm", _grids.Count, _intersections.Count);

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
            if (_intersections.Count == 0)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Cảnh báo", "Không tìm thấy giao điểm lưới trục hợp lệ nào trong dự án.");
                return;
            }

            bool doCols = ChkCreateColumns.IsChecked == true;
            bool doBeams = ChkCreateBeams.IsChecked == true;
            bool doFootings = ChkCreateFootings.IsChecked == true;

            if (!doCols && !doBeams && !doFootings)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Thông báo", "Vui lòng chọn ít nhất một tác vụ mô hình hóa.");
                return;
            }

            int colsCount = 0;
            int beamsCount = 0;
            int footingsCount = 0;

            List<FamilyInstance> createdColumns = null;

            try
            {
                // 1. Tạo Cột
                if (doCols)
                {
                    var colItem = CboColumnType.SelectedItem as ElementItem<FamilySymbol>;
                    var baseLvlItem = CboColumnBaseLevel.SelectedItem as ElementItem<Level>;
                    var topLvlItem = CboColumnTopLevel.SelectedItem as ElementItem<Level>;

                    if (colItem == null || baseLvlItem == null)
                    {
                        Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Vui lòng chọn Loại Cột và Tầng cơ sở (Base Level).");
                        return;
                    }

                    double bOffset = ParseDouble(TxtColumnBaseOffset.Text);
                    double tOffset = ParseDouble(TxtColumnTopOffset.Text);

                    createdColumns = QuickStructureService.CreateColumnsAtPoints(
                        _doc, _intersections, colItem.Item, baseLvlItem.Item, topLvlItem != null ? topLvlItem.Item : baseLvlItem.Item, bOffset, tOffset);

                    colsCount = createdColumns.Count;
                }

                // 2. Tạo Dầm
                if (doBeams)
                {
                    var beamItem = CboBeamType.SelectedItem as ElementItem<FamilySymbol>;
                    var beamLvlItem = CboBeamLevel.SelectedItem as ElementItem<Level>;

                    if (beamItem != null && beamLvlItem != null)
                    {
                        double zOffset = ParseDouble(TxtBeamZOffset.Text);
                        var createdBeams = QuickStructureService.CreateBeamsAlongGridSpans(
                            _doc, _grids, _intersections, beamItem.Item, beamLvlItem.Item, zOffset);

                        beamsCount = createdBeams.Count;
                    }
                }

                // 3. Tạo Móng dưới chân cột
                if (doFootings && createdColumns != null && createdColumns.Count > 0)
                {
                    var footingItem = CboFootingType.SelectedItem as ElementItem<FamilySymbol>;
                    var footingLvlItem = CboFootingLevel.SelectedItem as ElementItem<Level>;

                    if (footingItem != null && footingLvlItem != null)
                    {
                        double fOffset = ParseDouble(TxtFootingOffset.Text);
                        var createdFdns = QuickStructureService.CreateFootingsUnderColumns(
                            _doc, createdColumns, footingItem.Item, footingLvlItem.Item, fOffset);

                        footingsCount = createdFdns.Count;
                    }
                }

                string msg = string.Format("Tạo thành công:\n• {0} Cột kết cấu\n• {1} Dầm kết cấu\n• {2} Móng đơn", 
                    colsCount, beamsCount, footingsCount);

                Autodesk.Revit.UI.TaskDialog.Show("K-TOOLS — Quick Structure", msg);
                TxtStatus.Text = string.Format("Hoàn tất: {0} cột, {1} dầm, {2} móng.", colsCount, beamsCount, footingsCount);
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi thực thi", ex.ToString());
            }
        }

        private double ParseDouble(string text)
        {
            double val;
            if (double.TryParse((text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out val))
            {
                return val;
            }
            if (double.TryParse((text ?? "").Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out val))
            {
                return val;
            }
            return 0.0;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
