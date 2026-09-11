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
            var selected = GridFamilies.SelectedItems.Cast<FamilyFileInfo>().ToList();
            if (selected.Count == 0)
            {
                TaskDialog.Show("K-TOOLS", "Vui lòng chọn ít nhất một Family từ bảng.");
                return;
            }

            LoadFamilies(selected.Select(item => item.FullPath), false);
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            var selected = GridFamilies.SelectedItems.Cast<FamilyFileInfo>().ToList();
            if (selected.Count == 0)
            {
                TaskDialog.Show("K-TOOLS", "Vui lòng chọn ít nhất một Family để nạp lại.");
                return;
            }

            LoadFamilies(selected.Select(item => item.FullPath), true);
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

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn Family để nạp vào dự án",
                Filter = "Revit Family (*.rfa)|*.rfa",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                LoadFamilies(dialog.FileNames, false);
            }
        }

        private void GridFamilies_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GridFamilies.SelectedItem is FamilyFileInfo selected)
            {
                LoadFamilies(new[] { selected.FullPath }, false);
            }
        }

        private void LoadFamilies(IEnumerable<string> paths, bool overwrite)
        {
            var uniquePaths = paths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (uniquePaths.Count == 0) return;

            int loaded = 0;
            int skipped = 0;
            var failures = new List<string>();
            var options = new KhimFamilyLoadOptions(overwrite, overwrite);

            foreach (string path in uniquePaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                try
                {
                    if (!overwrite && Core.Family.FamilyManager.IsFamilyLoaded(_doc, fileName))
                    {
                        skipped++;
                        continue;
                    }

                    var family = Core.Family.FamilyManager.LoadFamilySafely(_doc, path, options);
                    if (family != null) loaded++;
                    else failures.Add(fileName);
                }
                catch (Exception ex)
                {
                    failures.Add(fileName + ": " + ex.Message);
                }
            }

            LoadData();
            string summary = $"Đã nạp: {loaded}\nĐã có trong dự án: {skipped}\nLỗi: {failures.Count}";
            if (failures.Count > 0)
            {
                summary += "\n\n" + string.Join("\n", failures.Take(8));
                if (failures.Count > 8) summary += $"\n... và {failures.Count - 8} file khác";
            }
            TaskDialog.Show("K-TOOLS - Load Family", summary);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
