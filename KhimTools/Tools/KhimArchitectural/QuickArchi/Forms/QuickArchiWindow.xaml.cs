using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Architectural.QuickArchi.Services;
using KhimTools.Architectural.QuickArchi.Models;

namespace KhimTools.Architectural.QuickArchi.Forms
{
    public partial class QuickArchiWindow : Window
    {
        public bool HasCompletedOperation { get; private set; }
        public bool OperationExecuted { get; private set; }
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
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // 1. Quét đường nét Curve từ selection
                _selectedCurves = QuickArchiService.GetSelectedOrModelCurves(_uidoc);
                TxtStatus.Text = _selectedCurves.Count + " curves đang chọn";

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

            var settings = new QuickArchiSettings
            {
                WallHeightMm = ParseDouble(TxtHeight.Text),
                WallOffsetMm = ParseDouble(TxtOffset.Text),
                IsStructural = ChkStructural.IsChecked == true,
                AutoCreateRooms = ChkAutoRooms.IsChecked == true
            };
            if (!settings.Validate(out string settingsError))
            {
                Autodesk.Revit.UI.TaskDialog.Show("Invalid settings", settingsError);
                return;
            }

            OperationExecuted = true;
            try
            {
                var wallResult = QuickArchiService.CreateWallsFromCurves(
                    _doc, _selectedCurves, wallItem.Item, levelItem.Item,
                    settings.WallHeightMm, settings.WallOffsetMm, settings.IsStructural);

                int roomsCount = 0;
                if (settings.AutoCreateRooms && _doc.ActiveView is ViewPlan vp)
                {
                    roomsCount = QuickArchiService.CreateRooms(_doc, vp);
                }

                string msg = string.Format("Tạo thành công:\n• {0} Đoạn Tường\n• {1} Phòng (Rooms)\n• {2} đường không hợp lệ/thất bại",
                    wallResult.CreatedWalls.Count, roomsCount, wallResult.Failed);
                Autodesk.Revit.UI.TaskDialog.Show("K-TOOLS — Quick Archi", msg);
                TxtStatus.Text = string.Format("Hoàn tất: {0} tường, {1} phòng, {2} lỗi.", wallResult.CreatedWalls.Count, roomsCount, wallResult.Failed);
                HasCompletedOperation = wallResult.CreatedWalls.Count > 0 || roomsCount > 0;
                if (!HasCompletedOperation)
                    Autodesk.Revit.UI.TaskDialog.Show("Quick Archi", "No architectural elements were created. Check the selected curves and active plan boundaries.");
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
