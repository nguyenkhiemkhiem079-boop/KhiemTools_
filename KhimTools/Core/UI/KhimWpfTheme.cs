using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KhimTools.Core.UI
{
    /// <summary>Shared visual language for every WPF surface hosted by Revit.</summary>
    public static class KhimWpfTheme
    {
        private static readonly Brush Canvas = BrushFrom("#F4F6F8");
        private static readonly Brush Surface = BrushFrom("#FFFFFF");
        private static readonly Brush SurfaceMuted = BrushFrom("#F8FAFC");
        private static readonly Brush Border = BrushFrom("#D8DEE6");
        private static readonly Brush Text = BrushFrom("#18212F");
        private static readonly Brush Muted = BrushFrom("#5E6B7A");
        private static readonly Brush Accent = BrushFrom("#087EA4");
        private static readonly Brush AccentSoft = BrushFrom("#E7F5F8");
        private static readonly Brush Danger = BrushFrom("#B42318");

        public static void Apply(FrameworkElement root)
        {
            if (root == null) return;
            System.Windows.Documents.TextElement.SetFontFamily(root, new FontFamily("Segoe UI"));
            root.UseLayoutRounding = true;
            root.SnapsToDevicePixels = true;
            if (root is Window window)
            {
                window.Background = Canvas;
                window.MinWidth = window.MinWidth > 0 ? window.MinWidth : 480;
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
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                StyleTree(VisualTreeHelper.GetChild(root, i));
        }

        private static void Style(DependencyObject element)
        {
            if (element is Button button)
            {
                button.MinHeight = 32;
                button.Padding = new Thickness(12, 6, 12, 6);
                button.FontWeight = FontWeights.SemiBold;
                button.Cursor = System.Windows.Input.Cursors.Hand;
                button.FocusVisualStyle = null;
                string role = button.Tag as string;
                if (role == "Primary") { button.Background = Accent; button.Foreground = Brushes.White; button.BorderBrush = Accent; }
                else if (role == "Danger") { button.Background = Surface; button.Foreground = Danger; button.BorderBrush = BrushFrom("#FDA29B"); }
                else if (button.Background == null || button.Background == SystemColors.ControlBrush)
                { button.Background = Surface; button.Foreground = Text; button.BorderBrush = Border; }
                button.BorderThickness = new Thickness(1);
            }
            else if (element is TextBox textBox)
            {
                textBox.MinHeight = 32; textBox.Padding = new Thickness(9, 5, 9, 5);
                textBox.Background = Surface; textBox.Foreground = Text; textBox.BorderBrush = Border;
                textBox.BorderThickness = new Thickness(1); textBox.VerticalContentAlignment = VerticalAlignment.Center;
            }
            else if (element is ComboBox comboBox)
            {
                comboBox.MinHeight = 32; comboBox.Padding = new Thickness(8, 4, 8, 4);
                comboBox.Background = Surface; comboBox.Foreground = Text; comboBox.BorderBrush = Border;
                comboBox.VerticalContentAlignment = VerticalAlignment.Center;
            }
            else if (element is DataGrid grid)
            {
                grid.Background = Surface; grid.Foreground = Text; grid.BorderBrush = Border;
                grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal; grid.HorizontalGridLinesBrush = Border;
                grid.RowBackground = Surface; grid.AlternatingRowBackground = SurfaceMuted;
                grid.RowHeight = 36; grid.ColumnHeaderHeight = 36;
            }
            else if (element is ListBox list) { list.Background = Surface; list.Foreground = Text; list.BorderBrush = Border; }
            else if (element is GroupBox group)
            { group.Foreground = Text; group.Background = Surface; group.BorderBrush = Border; group.Padding = new Thickness(12, 8, 12, 12); }
            else if (element is TabControl tabs) { tabs.Background = Canvas; tabs.BorderBrush = Border; }
            else if (element is TextBlock block && block.Foreground == null)
            { block.Foreground = block.FontSize <= 11 ? Muted : Text; block.TextTrimming = TextTrimming.CharacterEllipsis; }
            else if (element is ProgressBar progress) { progress.Foreground = Accent; progress.Background = AccentSoft; }
        }

        private static Brush BrushFrom(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
