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

        private void InitializeData()
        {
            try
            {
                _groups = FamilyDiscoveryService.DiscoverGroups(_settings);
                FamilyStatusService.UpdateStatuses(_doc, _groups);

                ListGroups.ItemsSource = _groups;
                if (_groups.Count > 0)
                {
                    ListGroups.SelectedIndex = 0;
                }

                UpdateOverallSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize family library:\n{ex.Message}", "KhimTools Family Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedGroup = ListGroups.SelectedItem as FamilyGroupModel;
            if (_selectedGroup == null) return;

            TxtSelectedGroupName.Text = _selectedGroup.DisplayName;
            TxtSelectedGroupRule.Text = _selectedGroup.RuleDescription;

            // Show Select/Deselect buttons only for selective groups (not Rebar)
            BtnSelectAllInGroup.Visibility = _selectedGroup.IsSelective ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            BtnClearAllInGroup.Visibility = _selectedGroup.IsSelective ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

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
            InitializeData();
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
                MessageBox.Show("Please select at least one family to load.", "KhimTools Family Manager", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    ProgressBarLoad.Value = current;
                    TxtOverallSummary.Text = $"Loading {famName} ({current}/{total})...";
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

                MessageBox.Show(report, "KhimTools Family Manager", MessageBoxButton.OK,
                    result.FailedCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during loading:\n{ex.Message}", "KhimTools Family Manager", MessageBoxButton.OK, MessageBoxImage.Error);
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
