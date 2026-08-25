using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace KhimTools.CalloutPro.Forms
{
    public partial class CalloutProWindow : System.Windows.Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public CalloutProWindow(UIDocument uidoc)
        {
            InitializeComponent();
            _uidoc = uidoc;
            _doc = uidoc.Document;

            LoadViewTemplates();
            LoadSheets();
            LoadTitleBlocks();
            TxtNamePattern.Text = $"Detail - {_doc.ActiveView.Name} - 01";
        }

        private void LoadViewTemplates()
        {
            try
            {
                var templates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();

                var list = new List<ViewSection> { null };
                list.AddRange(templates);

                ComboTemplates.ItemsSource = list;
                ComboTemplates.DisplayMemberPath = "Name";
                ComboTemplates.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadSheets()
        {
            try
            {
                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .OrderBy(s => s.SheetNumber)
                    .ToList();

                ComboSheets.ItemsSource = sheets;
                ComboSheets.DisplayMemberPath = "SheetNumber";
                if (sheets.Any())
                {
                    ComboSheets.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void LoadTitleBlocks()
        {
            try
            {
                var titleBlocks = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsElementType()
                    .Cast<FamilySymbol>()
                    .OrderBy(fs => fs.FamilyName)
                    .ToList();

                ComboTitleBlocks.ItemsSource = titleBlocks;
                ComboTitleBlocks.DisplayMemberPath = "Name";
                if (titleBlocks.Any())
                {
                    ComboTitleBlocks.SelectedIndex = 0;
                }
            }
            catch { }
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

        private void Radio_SheetModeChanged(object sender, RoutedEventArgs e)
        {
            if (PanelExistingSheet == null || PanelNewSheet == null) return;

            if (RadioExistingSheet.IsChecked == true)
            {
                PanelExistingSheet.Visibility = System.Windows.Visibility.Visible;
                PanelNewSheet.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                PanelExistingSheet.Visibility = System.Windows.Visibility.Collapsed;
                PanelNewSheet.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                string calloutName = TxtNamePattern.Text.Trim();
                if (string.IsNullOrEmpty(calloutName))
                {
                    MessageBox.Show("Vui lòng nhập tên Callout.");
                    return;
                }

                if (RadioNewSheet.IsChecked == true)
                {
                    if (string.IsNullOrEmpty(TxtNewSheetNumber.Text.Trim()) || string.IsNullOrEmpty(TxtNewSheetName.Text.Trim()))
                    {
                        MessageBox.Show("Vui lòng điền đầy đủ số hiệu và tên Sheet mới.");
                        return;
                    }
                }

                // Hướng dẫn vẽ Callout
                Hide();
                PickedBox picked = null;
                try
                {
                    picked = _uidoc.Selection.PickBox(PickBoxStyle.Directional, "Quét chọn vùng Callout chữ nhật trên mặt bằng");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    MessageBox.Show("Thao tác vẽ Callout đã bị hủy.");
                    Close();
                    return;
                }

                if (picked == null)
                {
                    Close();
                    return;
                }

                using (var tx = new Transaction(_doc, "K-TOOLS - Callout Pro"))
                {
                    tx.Start();

                    // 1. Tạo Callout View
                    var calloutType = new FilteredElementCollector(_doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(v => v.ViewFamily == ViewFamily.Detail);

                    if (calloutType == null)
                    {
                        calloutType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
                    }

                    if (calloutType == null)
                    {
                        MessageBox.Show("Không tìm thấy ViewFamilyType Detail/Section nào để tạo Callout.");
                        tx.RollBack();
                        Close();
                        return;
                    }

                    // Tạo Callout
                    var parentView = _doc.ActiveView;
                    View calloutView = null;
                    try
                    {
                        calloutView = ViewSection.CreateCallout(_doc, parentView.Id, calloutType.Id, picked.Min, picked.Max);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Revit API không hỗ trợ tạo Callout trên View này. Chi tiết: " + ex.Message);
                        tx.RollBack();
                        Close();
                        return;
                    }

                    // Đổi tên Callout
                    string finalName = calloutName;
                    int idx = 1;
                    while (ViewNameExists(finalName))
                    {
                        finalName = $"{calloutName} ({idx++})";
                    }
                    calloutView.Name = finalName;

                    // Gán View Template
                    var selectedTemplate = ComboTemplates.SelectedItem as ViewSection;
                    if (selectedTemplate != null)
                    {
                        calloutView.ViewTemplateId = selectedTemplate.Id;
                    }

                    // 2. Định nghĩa Sheet đích
                    ViewSheet targetSheet = null;
                    if (RadioExistingSheet.IsChecked == true)
                    {
                        targetSheet = ComboSheets.SelectedItem as ViewSheet;
                    }
                    else
                    {
                        // Tạo Sheet mới
                        var selectedTitleBlock = ComboTitleBlocks.SelectedItem as FamilySymbol;
                        ElementId tbId = selectedTitleBlock != null ? selectedTitleBlock.Id : ElementId.InvalidElementId;

                        targetSheet = ViewSheet.Create(_doc, tbId);
                        targetSheet.SheetNumber = TxtNewSheetNumber.Text.Trim();
                        targetSheet.Name = TxtNewSheetName.Text.Trim();
                    }

                    if (targetSheet == null)
                    {
                        MessageBox.Show("Không xác định được Sheet đích.");
                        tx.RollBack();
                        Close();
                        return;
                    }

                    // 3. Đặt Viewport vào Sheet
                    if (Viewport.CanAddViewToSheet(_doc, targetSheet.Id, calloutView.Id))
                    {
                        // Đặt ở tọa độ giữa bản vẽ (thường là khoảng 1.5, 1.0 feet)
                        XYZ center = new XYZ(1.5, 1.0, 0);
                        Viewport.Create(_doc, targetSheet.Id, calloutView.Id, center);
                    }
                    else
                    {
                        MessageBox.Show("Khung nhìn này đã được đặt trên một Sheet khác.");
                    }

                    tx.Commit();
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi thực thi Callout Pro: " + ex.Message);
                Close();
            }
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
