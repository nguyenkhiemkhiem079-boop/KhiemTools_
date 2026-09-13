using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.UI;
using KhimTools.OverrideTool.Services;
using KhimTools.OverrideTool.ViewModels;

namespace KhimTools.OverrideTool.Forms
{
    /// <summary>
    /// Code-behind cho GraphicOverdriveWindow — Modeless WPF Window.
    /// </summary>
    public partial class GraphicOverdriveWindow : Window
    {
        private readonly GraphicOverdriveViewModel _vm;

        public GraphicOverdriveWindow(UIApplication uiApp)
        {
            InitializeComponent();
            KhimTools.Core.UI.KhimWpfTheme.Apply(this);

            _vm = new GraphicOverdriveViewModel(uiApp);
            DataContext = _vm;

        }


        /// <summary>
        /// Mở Windows Forms ColorDialog chọn màu tự do.
        /// </summary>
        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.ColorDialog())
            {
                dlg.FullOpen = true;
                dlg.Color = System.Drawing.Color.FromArgb(
                    _vm.CustomColor.R,
                    _vm.CustomColor.G,
                    _vm.CustomColor.B);

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var picked = dlg.Color;
                    var wpfColor = Color.FromRgb(picked.R, picked.G, picked.B);

                    _vm.CustomColor = wpfColor;
                    btnPickColor.Background = new SolidColorBrush(wpfColor);
                    txbCustomHex.Text = $"#{picked.R:X2}{picked.G:X2}{picked.B:X2}";
                }
            }
        }

    }
}
