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

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (ToolList == null || ModuleFilter == null) return;
            string query = (ToolSearch?.Text ?? string.Empty).Trim();
            string module = (ModuleFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
            foreach (UIElement child in ToolList.Children)
            {
                if (child is Button button)
                {
                    string metadata = button.Tag as string ?? string.Empty;
                    int separator = metadata.IndexOf('|');
                    string itemModule = separator >= 0 ? metadata.Substring(0, separator) : string.Empty;
                    bool moduleMatch = module == "All" || string.Equals(module, itemModule, StringComparison.OrdinalIgnoreCase);
                    bool queryMatch = query.Length == 0 || metadata.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    button.Visibility = moduleMatch && queryMatch ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }
}
