using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core.Family;

namespace KhimTools.FamilyManager.Forms
{
    public partial class FamilyManagerWindow : Window
    {
        private readonly Document _doc;
        private List<FamilyFileInfo> _allFamilies = new List<FamilyFileInfo>();

        public FamilyManagerWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                _allFamilies = Core.Family.FamilyManager.ScanLibrary(_doc);

                // Populate categories dropdown
                var categories = new List<string> { "Tất cả danh mục" };
                var uniqueCats = _allFamilies.Select(f => f.Category).Distinct().OrderBy(c => c);
                categories.AddRange(uniqueCats);

                CboCategory.ItemsSource = categories;
                CboCategory.SelectedIndex = 0;

                TxtTotalFamilies.Text = _allFamilies.Count + " Families Sẵn Có";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Family Manager Error", "Lỗi khi quét thư viện family: " + ex.Message);
            }
        }

        private void ApplyFilter()
        {
            string query = (TxtSearch.Text ?? "").Trim();
            string selectedCat = CboCategory.SelectedItem as string;

            var filtered = _allFamilies.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(f => f.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrEmpty(selectedCat) && !string.Equals(selectedCat, "Tất cả danh mục", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(f => string.Equals(f.Category, selectedCat, StringComparison.OrdinalIgnoreCase));
            }

            GridFamilies.ItemsSource = filtered.ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void CboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridFamilies.SelectedItem as FamilyFileInfo;
            if (selected == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Thông báo", "Vui lòng chọn một Family từ bảng để nạp vào dự án.");
                return;
            }

            try
            {
                var options = new KhimFamilyLoadOptions(false, false);
                var fam = Core.Family.FamilyManager.LoadFamilySafely(_doc, selected.FullPath, options);
                if (fam != null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Thành công", $"Đã nạp thành công Family '{fam.Name}' vào dự án!");
                    LoadData();
                }
                else
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Cảnh báo", $"Không thể nạp Family '{selected.Name}'. Vui lòng kiểm tra lại file .rfa.");
                }
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Không thể nạp family: " + ex.Message);
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridFamilies.SelectedItem as FamilyFileInfo;
            if (selected == null)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Thông báo", "Vui lòng chọn một Family để tải lại và ghi đè.");
                return;
            }

            try
            {
                var options = new KhimFamilyLoadOptions(true, true);
                var fam = Core.Family.FamilyManager.LoadFamilySafely(_doc, selected.FullPath, options);
                if (fam != null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Thành công", $"Đã tải lại và ghi đè Family '{fam.Name}' thành công!");
                    LoadData();
                }
                else
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Cảnh báo", $"Ghi đè thất bại đối với Family '{selected.Name}'.");
                }
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Không thể ghi đè family: " + ex.Message);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridFamilies.SelectedItem as FamilyFileInfo;
            string targetPath = selected != null && File.Exists(selected.FullPath) 
                ? selected.FullPath 
                : Core.Family.FamilyPathResolver.GetProbeDirectories().FirstOrDefault(Directory.Exists);

            if (!string.IsNullOrEmpty(targetPath))
            {
                try
                {
                    if (File.Exists(targetPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{targetPath}\"");
                    }
                    else if (Directory.Exists(targetPath))
                    {
                        Process.Start("explorer.exe", $"\"{targetPath}\"");
                    }
                }
                catch (Exception ex)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Không thể mở Explorer: " + ex.Message);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
