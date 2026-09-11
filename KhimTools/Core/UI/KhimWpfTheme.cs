using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KhimTools.Core.UI
{
    public static class KhimWpfTheme
    {
        private static readonly Brush Surface = BrushFrom("#FFFFFF");
        private static readonly Brush Canvas = BrushFrom("#F6F7F9");
        private static readonly Brush Border = BrushFrom("#D7DBE0");
        private static readonly Brush Text = BrushFrom("#202124");
        private static readonly Brush Muted = BrushFrom("#5F6368");
        private static readonly Brush Secondary = BrushFrom("#EEF1F4");

        public static void Apply(FrameworkElement root)
        {
            if (root == null) return;

            if (root is Window window)
            {
                window.Background = Canvas;
                window.FontFamily = new FontFamily("Segoe UI");
                window.UseLayoutRounding = true;
                window.SnapsToDevicePixels = true;
                window.MinWidth = window.MinWidth > 0 ? window.MinWidth : 440;
                window.MinHeight = window.MinHeight > 0 ? window.MinHeight : 360;
            }

            root.Loaded -= Root_Loaded;
            root.Loaded += Root_Loaded;
        }

        private static void Root_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject root) StyleTree(root);
        }

        private static void StyleTree(DependencyObject root)
        {
            Style(root);
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) StyleTree(VisualTreeHelper.GetChild(root, i));
        }

        private static void Style(DependencyObject element)
        {
            if (element is Button button)
            {
                button.MinHeight = 32;
                button.Padding = new Thickness(12, 6, 12, 6);
                button.FontWeight = FontWeights.SemiBold;
                button.Cursor = System.Windows.Input.Cursors.Hand;
                if (button.Background == null || button.Background == SystemColors.ControlBrush)
                {
                    button.Background = Secondary;
                    button.Foreground = Text;
                    button.BorderBrush = Border;
                    button.BorderThickness = new Thickness(1);
                }
            }
            else if (element is TextBox textBox)
            {
                textBox.MinHeight = 30;
                textBox.Padding = new Thickness(8, 4, 8, 4);
                textBox.Background = Surface;
                textBox.Foreground = Text;
                textBox.BorderBrush = Border;
            }
            else if (element is ComboBox comboBox)
            {
                comboBox.MinHeight = 30;
                comboBox.Padding = new Thickness(7, 3, 7, 3);
                comboBox.Background = Surface;
                comboBox.Foreground = Text;
                comboBox.BorderBrush = Border;
            }
            else if (element is ListBox listBox)
            {
                listBox.Background = Surface;
                listBox.Foreground = Text;
                listBox.BorderBrush = Border;
            }
            else if (element is DataGrid grid)
            {
                grid.Background = Surface;
                grid.Foreground = Text;
                grid.BorderBrush = Border;
                grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
                grid.HorizontalGridLinesBrush = Border;
                grid.RowHeight = 32;
                grid.ColumnHeaderHeight = 34;
            }
            else if (element is GroupBox group)
            {
                group.Foreground = Text;
                group.Padding = new Thickness(12, 8, 12, 12);
            }
            else if (element is TextBlock textBlock && textBlock.Foreground == null)
            {
                textBlock.Foreground = textBlock.FontSize <= 11 ? Muted : Text;
            }
        }

        private static Brush BrushFrom(string color) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
