using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Architectural.QuickArchi.Services;

namespace KhimTools.Architectural.QuickArchi.Forms
{
    public partial class QuickArchiWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private List<Curve> _selectedCurves = new List<Curve>();
        private List<WallType> _wallTypes = new List<WallType>();
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

        public QuickArchiWindow(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // 1. Quét đường nét Curve từ selection
                _selectedCurves = QuickArchiService.GetSelectedOrModelCurves(_uidoc);
                TxtCurveStats.Text = _selectedCurves.Count + " Curves Đang Chọn";

                // 2. Thu thập WallTypes
                _wallTypes = QuickArchiService.GetWallTypes(_doc);
                var wallItems = _wallTypes.Select(w => new ElementItem<WallType>(w, w.Name)).ToList();
                CboWallType.ItemsSource = wallItems;
                if (wallItems.Count > 0) CboWallType.SelectedIndex = 0;

                // 3. Thu thập Levels
                _levels = QuickArchiService.GetLevels(_doc);
                var levelItems = _levels.Select(l => new ElementItem<Level>(l, l.Name)).ToList();
                CboLevel.ItemsSource = levelItems;
                if (levelItems.Count > 0) CboLevel.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi nạp dữ liệu", ex.Message);
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCurves.Count == 0)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Thông báo", "Vui lòng chọn các đường nét (Model Curves hoặc Detail Lines) trên khung nhìn trước khi chạy.");
                return;
            }

            var wallItem = CboWallType.SelectedItem as ElementItem<WallType>;
            var levelItem = CboLevel.SelectedItem as ElementItem<Level>;

            if (wallItem == null || levelItem == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Vui lòng chọn Loại Tường và Tầng cơ sở.");
                return;
            }

            double heightMm = ParseDouble(TxtHeight.Text);
            if (heightMm <= 0) heightMm = 3000.0;

            double offsetMm = ParseDouble(TxtOffset.Text);
            bool isStructural = ChkStructural.IsChecked == true;

            try
            {
                var createdWalls = QuickArchiService.CreateWallsFromCurves(
                    _doc, _selectedCurves, wallItem.Item, levelItem.Item, heightMm, offsetMm, isStructural);

                int roomsCount = 0;
                if (ChkAutoRooms.IsChecked == true && _doc.ActiveView is ViewPlan vp)
                {
                    roomsCount = QuickArchiService.CreateRoomsAndTags(_doc, vp);
                }

                string msg = string.Format("Tạo thành công:\n• {0} Đoạn Tường\n• {1} Phòng (Rooms)", createdWalls.Count, roomsCount);
                Autodesk.Revit.UI.TaskDialog.Show("K-TOOLS — Quick Archi", msg);
                TxtStatus.Text = string.Format("Hoàn tất: {0} tường, {1} phòng.", createdWalls.Count, roomsCount);
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi thực thi", ex.Message);
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
