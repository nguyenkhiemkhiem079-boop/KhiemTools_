using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using KhimTools.FamilyManager.Models;
using KhimTools.FamilyManager.Services;

namespace KhimTools.FamilyManager.Views
{
    public class SourceItemViewModel
    {
        public FamilyLibrarySource Source { get; }

        public string DisplayName => Source.DisplayName;
        public FamilyGroupType LogicalGroup => Source.LogicalGroup;
        public string RootPathsText => string.Join("; ", Source.RootPaths ?? new List<string>());

        public SourceItemViewModel(FamilyLibrarySource source)
        {
            Source = source;
        }
    }

    public partial class LibrarySourcesDialog : Window
    {
        private readonly FamilyManagerSettings _settings;
        public ObservableCollection<SourceItemViewModel> Sources { get; } = new ObservableCollection<SourceItemViewModel>();

        public LibrarySourcesDialog(FamilyManagerSettings settings)
        {
            InitializeComponent();
            _settings = settings ?? FamilyManagerSettings.Load();

            LoadSources();
        }

        private void LoadSources()
        {
            Sources.Clear();
            if (_settings.Sources != null)
            {
                foreach (var src in _settings.Sources)
                {
                    Sources.Add(new SourceItemViewModel(src));
                }
            }

            ListSources.ItemsSource = Sources;
            UpdateButtonsState();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ListSources_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateButtonsState();
        }

        private void UpdateButtonsState()
        {
            int index = ListSources.SelectedIndex;
            BtnRemove.IsEnabled = index >= 0;
            BtnMoveUp.IsEnabled = index > 0;
            BtnMoveDown.IsEnabled = index >= 0 && index < Sources.Count - 1;
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a folder containing Revit Family (.rfa) files";
                dialog.ShowNewFolderButton = false;

                var res = dialog.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    string selectedPath = dialog.SelectedPath;
                    string folderName = Path.GetFileName(selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(folderName)) folderName = selectedPath;

                    var newSource = new FamilyLibrarySource
                    {
                        Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        DisplayName = folderName + " (Custom)",
                        LogicalGroup = FamilyGroupType.Structure,
                        RootPaths = new List<string> { selectedPath },
                        Priority = 110,
                        IsEnabled = true
                    };

                    Sources.Insert(0, new SourceItemViewModel(newSource));
                    ListSources.SelectedIndex = 0;
                    UpdateButtonsState();
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = ListSources.SelectedIndex;
            if (index >= 0 && index < Sources.Count)
            {
                Sources.RemoveAt(index);
                if (Sources.Count > 0)
                {
                    ListSources.SelectedIndex = Math.Min(index, Sources.Count - 1);
                }
                UpdateButtonsState();
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = ListSources.SelectedIndex;
            if (index > 0)
            {
                var item = Sources[index];
                Sources.RemoveAt(index);
                Sources.Insert(index - 1, item);
                ListSources.SelectedIndex = index - 1;
                UpdateButtonsState();
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = ListSources.SelectedIndex;
            if (index >= 0 && index < Sources.Count - 1)
            {
                var item = Sources[index];
                Sources.RemoveAt(index);
                Sources.Insert(index + 1, item);
                ListSources.SelectedIndex = index + 1;
                UpdateButtonsState();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Update settings priorities according to current order
            int priority = 150;
            var updatedSources = new List<FamilyLibrarySource>();
            foreach (var vm in Sources)
            {
                vm.Source.Priority = priority;
                priority -= 5;
                updatedSources.Add(vm.Source);
            }

            _settings.Sources = updatedSources;
            _settings.Save();

            FamilyDiscoveryService.InvalidateCache();
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
