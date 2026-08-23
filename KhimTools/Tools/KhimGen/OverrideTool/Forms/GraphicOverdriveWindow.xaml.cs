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

            _vm = new GraphicOverdriveViewModel(uiApp);
            DataContext = _vm;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RenderColorPresets();
        }

        /// <summary>
        /// Duyệt ItemsControl tìm các Button ColorChip và gán Background = SolidColorBrush đúng màu preset.
        /// </summary>
        private void RenderColorPresets()
        {
            var presets = _vm.Presets;
            if (presets == null) return;

            var itemsControl = FindVisualChild<ItemsControl>(this);
            if (itemsControl == null) return;

            itemsControl.UpdateLayout();

            for (int i = 0; i < presets.Count && i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                if (container == null) continue;

                container.ApplyTemplate();
                var btn = FindVisualChild<Button>(container);
                if (btn == null) continue;

                var p = presets[i];
                btn.Background = new SolidColorBrush(
                    Color.FromRgb((byte)p.R, (byte)p.G, (byte)p.B));
            }
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

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}