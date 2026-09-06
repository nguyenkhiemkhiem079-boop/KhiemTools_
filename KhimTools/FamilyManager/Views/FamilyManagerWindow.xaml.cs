using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using KhimTools.FamilyManager.Models;
using KhimTools.FamilyManager.Services;

namespace KhimTools.FamilyManager.Views
{
    public partial class FamilyManagerWindow : Window
    {
        private readonly Document _doc;
        private FamilyManagerSettings _settings;
        private List<FamilyGroupModel> _groups = new List<FamilyGroupModel>();
        private FamilyGroupModel _selectedGroup;
        private ICollectionView _currentView;

        public FamilyManagerWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            _settings = FamilyManagerSettings.Load();

            Loaded += (s, e) => InitializeData();
        }

        private void InitializeData(bool forceRescan = false)
        {
            try
            {
                _groups = FamilyDiscoveryService.DiscoverGroups(_settings, forceRescan);
                FamilyStatusService.UpdateStatuses(_doc, _groups);

                ListGroups.ItemsSource = _groups;
                if (_groups.Count > 0)
                {
                    ListGroups.SelectedIndex = 0;
                }

                // Check for scan warnings or corrupt settings fallback
                if (_settings.WasFallbackToDefault)
                {
                    BannerWarnings.Visibility = System.Windows.Visibility.Visible;
                    TxtWarningNotice.Text = "⚠️ Settings fell back to defaults due to corrupted configuration (backed up).";
                }
                else if (FamilyDiscoveryService.LastScanWarnings.Count > 0)
                {
                    BannerWarnings.Visibility = System.Windows.Visibility.Visible;
                    TxtWarningNotice.Text = $"⚠️ {FamilyDiscoveryService.LastScanWarnings.Count} library directory warning(s) detected during scan.";
                }
                else
                {
                    BannerWarnings.Visibility = System.Windows.Visibility.Collapsed;
                }

                UpdateOverallSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize family library:\n{ex.Message}", "K-TOOLS Family Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnViewWarnings_Click(object sender, RoutedEventArgs e)
        {
            var msgs = new List<string>();
            if (_settings.WasFallbackToDefault && !string.IsNullOrEmpty(_settings.LastLoadError))
            {
                msgs.Add($"Settings Error: {_settings.LastLoadError}");
            }
            msgs.AddRange(FamilyDiscoveryService.LastScanWarnings);

            string details = msgs.Count > 0 ? string.Join("\n• ", msgs) : "No diagnostic warnings.";
            MessageBox.Show($"Diagnostic scan warnings:\n• {details}", "K-TOOLS Diagnostics", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnManageSources_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LibrarySourcesDialog(_settings);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                FamilyDiscoveryService.InvalidateCache();
                InitializeData(forceRescan: true);
            }
        }

        private void ListGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedGroup = ListGroups.SelectedItem as FamilyGroupModel;
            if (_selectedGroup == null) return;

            TxtSelectedGroupName.Text = _selectedGroup.DisplayName;
            TxtSelectedGroupRule.Text = _selectedGroup.RuleDescription;

            bool isRebar = _selectedGroup.GroupType == FamilyGroupType.Rebar;

            // Rebar suite uses single info card and single toggle instead of per-shape checkboxes
            if (isRebar)
            {
                CardRebarInfo.Visibility = System.Windows.Visibility.Visible;
                ChkRebarSingleToggle.IsChecked = _selectedGroup.IsChecked == true;
                ColCheckbox.Visibility = System.Windows.Visibility.Collapsed;
                BtnSelectAllInGroup.Visibility = System.Windows.Visibility.Collapsed;
                BtnClearAllInGroup.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                CardRebarInfo.Visibility = System.Windows.Visibility.Collapsed;
                ColCheckbox.Visibility = System.Windows.Visibility.Visible;
                BtnSelectAllInGroup.Visibility = _selectedGroup.IsSelective ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                BtnClearAllInGroup.Visibility = _selectedGroup.IsSelective ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            _currentView = CollectionViewSource.GetDefaultView(_selectedGroup.Families);
            if (_currentView != null)
            {
                _currentView.Filter = FilterFamilyItem;
            }

            GridFamilies.ItemsSource = _currentView;

            // Listen to child selection changes to update overall summary
            foreach (var fam in _selectedGroup.Families)
            {
                fam.PropertyChanged -= OnFamilyItemPropertyChanged;
                fam.PropertyChanged += OnFamilyItemPropertyChanged;
            }

            UpdateOverallSummary();
        }

        private void ChkRebarSingleToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ChkRebarSingleToggle.IsChecked == true;
            _selectedGroup?.SetAllChildren(isChecked);
            UpdateOverallSummary();
        }

        private void OnFamilyItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyItemModel.IsSelected))
            {
                UpdateOverallSummary();
            }
        }

        private bool FilterFamilyItem(object obj)
        {
            if (!(obj is FamilyItemModel item)) return false;

            string query = TxtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(query)) return true;

            return item.FamilyName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentView?.Refresh();
        }

        private void BtnSelectAllInGroup_Click(object sender, RoutedEventArgs e)
        {
            _selectedGroup?.SetAllChildren(true);
            UpdateOverallSummary();
        }

        private void BtnClearAllInGroup_Click(object sender, RoutedEventArgs e)
        {
            _selectedGroup?.SetAllChildren(false);
            UpdateOverallSummary();
        }

        private void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            FamilyDiscoveryService.InvalidateCache();
            InitializeData(forceRescan: true);
        }

        private void UpdateOverallSummary()
        {
            int totalSelected = _groups.Sum(g => g.SelectedCount);
            int totalLoaded = _groups.Sum(g => g.LoadedCount);
            int totalDiscovered = _groups.Sum(g => g.TotalCount);

            TxtOverallSummary.Text = $"Selected to load: {totalSelected} families | In project: {totalLoaded}/{totalDiscovered} total components";
        }

        private void BtnLoadSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _groups.SelectMany(g => g.Families).Where(f => f.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one family to load.", "K-TOOLS Family Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool overwrite = ChkOverwriteExisting.IsChecked == true;

            BtnLoadSelected.IsEnabled = false;
            ProgressBarLoad.Visibility = System.Windows.Visibility.Visible;
            ProgressBarLoad.Value = 0;
            ProgressBarLoad.Maximum = selectedItems.Count;

            try
            {
                var result = FamilyLoaderService.LoadFamilies(_doc, selectedItems, overwrite, (famName, current, total) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBarLoad.Value = current;
                        TxtOverallSummary.Text = $"Loading {famName} ({current}/{total})...";
                    });
                });

                // Refresh document statuses
                FamilyStatusService.UpdateStatuses(_doc, _groups);
                UpdateOverallSummary();

                string report = $"Load completed!\n\n" +
                                $"• Successfully loaded: {result.LoadedCount}\n" +
                                $"• Already in project / up to date: {result.UpToDateCount}\n" +
                                $"• Failures: {result.FailedCount}";

                if (result.FailedCount > 0)
                {
                    report += "\n\nFailed items:\n" + string.Join("\n", result.Failures.Select(f => $"• {f.Key}: {f.Value}"));
                }

                MessageBox.Show(report, "K-TOOLS Family Manager", MessageBoxButton.OK,
                    result.FailedCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during loading:\n{ex.Message}", "K-TOOLS Family Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnLoadSelected.IsEnabled = true;
                ProgressBarLoad.Visibility = System.Windows.Visibility.Collapsed;
                UpdateOverallSummary();
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
    }
}
