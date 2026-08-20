using System;
using System.Windows.Controls;
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
    }
}
