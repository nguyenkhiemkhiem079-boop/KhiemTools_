using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace KhimTools.SectionBox.Forms
{
    public partial class SectionBoxWindow : System.Windows.Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private List<ElementId> _selectedElementIds = new List<ElementId>();

        public SectionBoxWindow(UIDocument uidoc)
        {
            InitializeComponent();
            _uidoc = uidoc;
            _doc = uidoc.Document;

            LoadLevels();
            LoadViewTemplates();
            LoadCurrentSelection();
        }

        private void LoadLevels()
        {
            try
            {
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                ComboLevels.ItemsSource = levels;
                ComboLevels.DisplayMemberPath = "Name";
                if (levels.Any())
                {
                    ComboLevels.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi load Level: " + ex.Message);
            }
        }

        private void LoadViewTemplates()
        {
            try
            {
                var templates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();

                var list = new List<View3D> { null }; // Option for No Template
                list.AddRange(templates);

                ComboTemplates.ItemsSource = list;
                ComboTemplates.DisplayMemberPath = "Name";
                ComboTemplates.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadCurrentSelection()
        {
            var selIds = _uidoc.Selection.GetElementIds().ToList();
            if (selIds.Any())
            {
                _selectedElementIds = selIds;
                TxtSelectionStatus.Text = $"Đã chọn sẵn {selIds.Count} cấu kiện";
                RadioSelection.IsChecked = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Radio_ModeChanged(object sender, RoutedEventArgs e)
        {
            if (PanelLevel == null || PanelSelection == null) return;

            if (RadioLevel.IsChecked == true)
            {
                PanelLevel.Visibility = System.Windows.Visibility.Visible;
                PanelSelection.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                PanelLevel.Visibility = System.Windows.Visibility.Collapsed;
                PanelSelection.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void BtnPickElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                var refs = _uidoc.Selection.PickObjects(ObjectType.Element, "Chọn các cấu kiện để tạo Section Box");
                if (refs != null && refs.Any())
                {
                    _selectedElementIds = refs.Select(r => r.ElementId).ToList();
                    TxtSelectionStatus.Text = $"Đã chọn {refs.Count} cấu kiện";
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Thao tác hủy quét chọn
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi chọn cấu kiện: " + ex.Message);
            }
            finally
            {
                Show();
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                double xMin = 0, xMax = 0, yMin = 0, yMax = 0, zMin = 0, zMax = 0;
                string defaultViewName = "3D - Section - ";

                if (RadioLevel.IsChecked == true)
                {
                    var selectedLevel = ComboLevels.SelectedItem as Level;
                    if (selectedLevel == null)
                    {
                        MessageBox.Show("Vui lòng chọn một Level.");
                        return;
                    }

                    if (!double.TryParse(TxtTopOffset.Text, out double topOffsetMm) ||
                        !double.TryParse(TxtBottomOffset.Text, out double bottomOffsetMm))
                    {
                        MessageBox.Show("Khoảng cách Offset phải là số hợp lệ.");
                        return;
                    }

                    double elevation = selectedLevel.Elevation;
                    double topOffset = topOffsetMm / 304.8;
                    double bottomOffset = bottomOffsetMm / 304.8;
                    double levelHeight = GetLevelHeight(selectedLevel);

                    zMin = elevation + bottomOffset;
                    zMax = elevation + levelHeight + topOffset;

                    // Lấy giới hạn X, Y từ CropBox của Active View hoặc BoundingBox mô hình
                    var activeView = _doc.ActiveView;
                    if (activeView.CropBox != null && activeView.CropBoxActive)
                    {
                        xMin = activeView.CropBox.Min.X;
                        xMax = activeView.CropBox.Max.X;
                        yMin = activeView.CropBox.Min.Y;
                        yMax = activeView.CropBox.Max.Y;
                    }
                    else
                    {
                        // Giới hạn mô hình mặc định
                        xMin = -150; xMax = 150;
                        yMin = -150; yMax = 150;
                    }

                    defaultViewName += selectedLevel.Name;
                }
                else
                {
                    if (!_selectedElementIds.Any())
                    {
                        MessageBox.Show("Vui lòng chọn ít nhất một cấu kiện.");
                        return;
                    }

                    xMin = double.MaxValue; xMax = double.MinValue;
                    yMin = double.MaxValue; yMax = double.MinValue;
                    zMin = double.MaxValue; zMax = double.MinValue;

                    foreach (var id in _selectedElementIds)
                    {
                        var elem = _doc.GetElement(id);
                        var bbox = elem.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            if (bbox.Min.X < xMin) xMin = bbox.Min.X;
                            if (bbox.Max.X > xMax) xMax = bbox.Max.X;
                            if (bbox.Min.Y < yMin) yMin = bbox.Min.Y;
                            if (bbox.Max.Y > yMax) yMax = bbox.Max.Y;
                            if (bbox.Min.Z < zMin) zMin = bbox.Min.Z;
                            if (bbox.Max.Z > zMax) zMax = bbox.Max.Z;
                        }
                    }

                    if (xMin == double.MaxValue)
                    {
                        MessageBox.Show("Không lấy được Bounding Box của cấu kiện đã chọn.");
                        return;
                    }

                    // Mở rộng ra 1 feet (30cm) cho dễ nhìn
                    xMin -= 1.0; xMax += 1.0;
                    yMin -= 1.0; yMax += 1.0;
                    zMin -= 1.0; zMax += 1.0;

                    defaultViewName += "Selection";
                }

                // Hậu tố
                string suffix = TxtViewNameSuffix.Text.Trim();
                if (!string.IsNullOrEmpty(suffix))
                {
                    defaultViewName += "_" + suffix;
                }

                // Thực thi tạo View
                using (var tx = new Transaction(_doc, "K-TOOLS - Create Section Box View"))
                {
                    tx.Start();

                    var viewFamilyType = new FilteredElementCollector(_doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                    if (viewFamilyType == null)
                    {
                        MessageBox.Show("Không tìm thấy ViewFamilyType 3D trong dự án.");
                        tx.RollBack();
                        return;
                    }

                    var view3d = View3D.CreateIsometric(_doc, viewFamilyType.Id);
                    
                    // Naming
                    string finalName = defaultViewName;
                    int i = 1;
                    while (ViewNameExists(finalName))
                    {
                        finalName = $"{defaultViewName} ({i++})";
                    }
                    view3d.Name = finalName;

                    // Section Box
                    var bboxBox = new BoundingBoxXYZ
                    {
                        Min = new XYZ(xMin, yMin, zMin),
                        Max = new XYZ(xMax, yMax, zMax)
                    };
                    view3d.IsSectionBoxActive = true;
                    view3d.SetSectionBox(bboxBox);

                    // View Template
                    var selectedTemplate = ComboTemplates.SelectedItem as View3D;
                    if (selectedTemplate != null)
                    {
                        view3d.ViewTemplateId = selectedTemplate.Id;
                    }

                    tx.Commit();

                    // Switch sang View mới
                    _uidoc.ActiveView = view3d;
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tạo Section Box: " + ex.Message);
            }
        }

        private double GetLevelHeight(Level level)
        {
            try
            {
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                int idx = levels.FindIndex(l => l.Id == level.Id);
                if (idx >= 0 && idx < levels.Count - 1)
                {
                    return levels[idx + 1].Elevation - level.Elevation;
                }
            }
            catch { }
            return 4000.0 / 304.8; // 4m mặc định nếu là tầng mái
        }

        private bool ViewNameExists(string name)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
