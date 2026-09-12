using System;
using System.Windows.Controls;
using System.Windows;
using Autodesk.Revit.UI;
using KhimTools.Tools.Workspace.ViewModels;

namespace KhimTools.Tools.Workspace.Views
{
    public partial class KhimWorkspacePane : Page, IDockablePaneProvider
    {
        public static readonly Guid PaneGuid = new Guid("8A72F671-508E-4573-A33D-502DF04F34A1");
        public static readonly DockablePaneId PaneId = new DockablePaneId(PaneGuid);

        public KhimWorkspaceViewModel ViewModel { get; }

        public KhimWorkspacePane()
        {
            InitializeComponent();
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);
            ViewModel = new KhimWorkspaceViewModel();
            DataContext = ViewModel;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        private void ToolSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

        private void ModuleTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySearch();

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            ToolSearch.Clear();
            ToolSearch.Focus();
        }

        private void ApplySearch()
        {
            if (ModuleTabs == null) return;
            string query = (ToolSearch?.Text ?? string.Empty).Trim();
            if (!(ModuleTabs.SelectedContent is ScrollViewer scroll) || !(scroll.Content is StackPanel panel)) return;
            foreach (UIElement child in panel.Children)
            {
                if (child is Button button)
                {
                    string keywords = button.Tag as string ?? string.Empty;
                    button.Visibility = query.Length == 0 || keywords.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }
}
