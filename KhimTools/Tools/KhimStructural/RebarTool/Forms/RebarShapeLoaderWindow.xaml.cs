using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>
    /// Item hiển thị trong DataGrid danh sách Rebar Shape.
    /// </summary>
    public class RebarShapeItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isLoaded;
        private string _filePath;
        private string _statusText;
        private Brush _statusBgColor;
        private Brush _statusFgColor;

        public string Name { get; set; }
        public string Description { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public bool IsLoaded
        {
            get { return _isLoaded; }
            set
            {
                if (_isLoaded != value)
                {
                    _isLoaded = value;
                    OnPropertyChanged("IsLoaded");
                    UpdateStatusDisplay();
                }
            }
        }

        public string FilePath
        {
            get { return _filePath; }
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged("FilePath");
                    UpdateStatusDisplay();
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged("StatusText");
                }
            }
        }

        public Brush StatusBgColor
        {
            get { return _statusBgColor; }
            private set
            {
                if (_statusBgColor != value)
                {
                    _statusBgColor = value;
                    OnPropertyChanged("StatusBgColor");
                }
            }
        }

        public Brush StatusFgColor
        {
            get { return _statusFgColor; }
            private set
            {
                if (_statusFgColor != value)
                {
                    _statusFgColor = value;
                    OnPropertyChanged("StatusFgColor");
                }
            }
        }

        public void UpdateStatusDisplay()
        {
            if (_isLoaded)
            {
                StatusText = "Đã có trong Project";
                StatusBgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 56, 43));
                StatusFgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            }
            else if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
            {
                StatusText = "Sẵn sàng nạp";
                StatusBgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 51, 74));
                StatusFgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 180, 216));
            }
            else
            {
                StatusText = "Không tìm thấy file";
                StatusBgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 27, 34));
                StatusFgColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Interaction logic for RebarShapeLoaderWindow.xaml
    /// </summary>
    public partial class RebarShapeLoaderWindow : Window
    {
        private readonly Document _doc;
        private List<RebarShapeItemViewModel> _allShapes = new List<RebarShapeItemViewModel>();

        private static readonly Dictionary<string, string> ShapeDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "JP_T00", "Thanh thẳng dọc / thép chủ (Straight Bar)" },
            { "JP_T02", "Thanh neo góc 45° / Crosstie uốn 180°" },
            { "JP_T03", "Thanh neo ngắn vuông góc 90°" },
            { "JP_T04", "Thanh hook kháng chấn 135° (Seismic Hook)" },
            { "JP_T05", "Thanh hook uốn 1 đầu góc tù" },
            { "JP_T06", "Thanh hook uốn lượn liên kết" },
            { "JP_T07", "Thanh neo chéo dầm móng" },
            { "JP_T11", "Thanh chữ L hook 90° phải (Starter Bar / Dowel)" },
            { "JP_T11a", "Thanh chữ L hook 90° trái (Mirror Starter Bar)" },
            { "JP_T12", "Thanh chữ L góc uốn mở rộng" },
            { "JP_T13", "Thanh chữ L góc nhọn" },
            { "JP_T14", "Thanh bẻ góc kép lệch" },
            { "JP_T15", "Thanh neo góc chân cột" },
            { "JP_T16", "Thanh neo uốn lượn 1 đầu" },
            { "JP_T17", "Thanh neo đầu đặc thù" },
            { "JP_T20", "U-bar (2 đầu bẻ 90° cùng chiều)" },
            { "JP_T21", "Z-bar (2 đầu bẻ 90° ngược chiều)" },
            { "JP_T22", "S-bar (Cốt thép chữ S)" },
            { "JP_T23", "U-bar góc vát nghiêng" },
            { "JP_T24", "U-bar chân rộng" },
            { "JP_T25", "U-bar chân hẹp" },
            { "JP_T26", "U-bar có móc neo 2 đầu" },
            { "JP_T27", "U-bar đáy vát gia cường góc" },
            { "JP_T28", "Thanh uốn 2 đầu góc tù" },
            { "JP_T29", "Thanh bẻ 3 khúc đối xứng" },
            { "JP_T31", "Ghế kê sàn / Chân chó (Slab Chair Bar)" },
            { "JP_T32", "Thanh uốn bậc thang giật cấp" },
            { "JP_T34", "Thanh uốn nhiều nhịp" },
            { "JP_T35", "Thanh uốn lượn liên tục" },
            { "JP_T36", "Thanh uốn cong hình học" },
            { "JP_T38", "Thanh uốn zíc zắc giằng" },
            { "JP_T41", "Thanh đa chân 3 nhánh (Multi-leg)" },
            { "JP_T44", "Thanh đa chân 4 nhánh" },
            { "JP_T46", "Khung thép đa đoạn liên kết" },
            { "JP_T47", "Khung cốt thép hộp giằng" },
            { "JP_T48", "Khung liên kết chân cột móng" },
            { "JP_T49", "Khung gia cường đài móng" },
            { "JP_T51", "Đai kín chữ nhật bo góc, hook 90°/135° (Closed Stirrup)" },
            { "JP_T63", "Đai hở chữ U (Open Stirrup / Link)" },
            { "JP_T67", "Đai hở 1 nhánh thẳng (Hairpin Link)" },
            { "JP_T68", "Đai móc phụ 2 đầu hook (Crosslink)" },
            { "JP_T75", "Vòng đai tròn kín (Circular Closed Stirrup)" },
            { "JP_T80", "Đai hình thoi / Bát giác (Diamond Stirrup)" }
        };

        public RebarShapeLoaderWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var loadedNames = RebarShapeLibrary.GetLoadedShapeNames(_doc);

                _allShapes.Clear();
                foreach (var shapeName in RebarShapeLibrary.AllStandardShapes)
                {
                    string desc;
                    if (!ShapeDescriptions.TryGetValue(shapeName, out desc))
                    {
                        desc = "Rebar Shape tiêu chuẩn BS 8666 / JIS";
                    }

                    string rfaPath = RebarShapeLibrary.ResolveRfaPath(shapeName);
                    bool isLoaded = loadedNames.Any(n => n.Equals(shapeName, StringComparison.OrdinalIgnoreCase) ||
                                                         n.StartsWith(shapeName + "_", StringComparison.OrdinalIgnoreCase));

                    var item = new RebarShapeItemViewModel
                    {
                        Name = shapeName,
                        Description = desc,
                        IsLoaded = isLoaded,
                        FilePath = rfaPath ?? "Chưa định vị được file",
                        IsSelected = !isLoaded // Mặc định tích chọn những shape chưa nạp
                    };
                    item.UpdateStatusDisplay();
                    _allShapes.Add(item);
                }

                ApplyFilter();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Shape Loader", "Lỗi khởi tạo danh sách shape: " + ex.Message);
            }
        }

        private void ApplyFilter()
        {
            string query = (TxtSearch.Text ?? "").Trim();
            var filtered = _allShapes.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(s =>
                    s.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            DgShapes.ItemsSource = filtered.ToList();
        }

        private void UpdateSummary()
        {
            int loadedCount = _allShapes.Count(s => s.IsLoaded);
            int total = _allShapes.Count;
            TxtCountSummary.Text = string.Format("Đã nạp: {0} / {1} Shapes", loadedCount, total);
            TxtStatsHeader.Text = string.Format("{0} / {1} Shapes Trong Model", loadedCount, total);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool select = _allShapes.Any(s => !s.IsSelected);
            foreach (var s in _allShapes)
            {
                s.IsSelected = select;
            }
            BtnSelectAll.Content = select ? "Bỏ chọn tất cả" : "Chọn tất cả";
        }

        private void BtnLoadAll_Click(object sender, RoutedEventArgs e)
        {
            ExecuteLoadShapes(_allShapes);
        }

        private void BtnLoadSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allShapes.Where(s => s.IsSelected).ToList();
            if (selected.Count == 0)
            {
                TaskDialog.Show("Rebar Shape Loader", "Vui lòng tích chọn ít nhất một Rebar Shape trong bảng để nạp.");
                return;
            }

            ExecuteLoadShapes(selected);
        }

        private void BtnPreloadCommon_Click(object sender, RoutedEventArgs e)
        {
            string[] common = { "JP_T00", "JP_T02", "JP_T11", "JP_T12", "JP_T21", "JP_T27", "JP_T51", "JP_T68", "JP_T75", "JP_T80" };
            var targets = _allShapes.Where(s => common.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            ExecuteLoadShapes(targets);
        }

        private void ExecuteLoadShapes(List<RebarShapeItemViewModel> itemsToLoad)
        {
            if (_doc == null || _doc.IsReadOnly)
            {
                TaskDialog.Show("Rebar Shape Loader", "Tài liệu Revit đang ở chế độ Read-Only hoặc chưa mở.");
                return;
            }

            int newlyLoaded = 0;
            int alreadyLoaded = 0;
            int failed = 0;

            PrgProgress.Visibility = Visibility.Visible;
            TxtStatus.Text = "Đang nạp " + itemsToLoad.Count + " Rebar Shapes vào dự án...";

            try
            {
                using (var tx = new Transaction(_doc, "K-TOOLS: Load Rebar Shapes"))
                {
                    tx.Start();

                    foreach (var item in itemsToLoad)
                    {
                        if (item.IsLoaded)
                        {
                            alreadyLoaded++;
                            continue;
                        }

                        var shape = RebarShapeLibrary.GetOrLoadShape(_doc, item.Name);
                        if (shape != null)
                        {
                            newlyLoaded++;
                            item.IsLoaded = true;
                            item.IsSelected = false;
                        }
                        else
                        {
                            failed++;
                        }
                    }

                    tx.Commit();
                }

                UpdateSummary();
                TxtStatus.Text = string.Format("Hoàn tất: Nạp mới {0}, Đã có sẵn {1}, Thất bại {2}.", newlyLoaded, alreadyLoaded, failed);
                TaskDialog.Show("K-TOOLS Rebar Shape Loader",
                    string.Format("Đã nạp thành công các Rebar Shape vào dự án:\n- Nạp mới: {0}\n- Đã có sẵn: {1}\n- Thất bại: {2}",
                        newlyLoaded, alreadyLoaded, failed));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Shape Loader Error", "Lỗi trong quá trình nạp family: " + ex.Message);
                TxtStatus.Text = "Lỗi: " + ex.Message;
            }
            finally
            {
                PrgProgress.Visibility = Visibility.Collapsed;
            }
        }
    }
}
