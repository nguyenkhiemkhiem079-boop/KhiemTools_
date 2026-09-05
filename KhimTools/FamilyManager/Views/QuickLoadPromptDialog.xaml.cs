using System.Windows;
using System.Windows.Input;

namespace KhimTools.FamilyManager.Views
{
    public enum QuickLoadPromptAction
    {
        Cancel = 0,
        LoadSingleFamily = 1,
        LoadAllRebar = 2,
        OpenFamilyManager = 3
    }

    public partial class QuickLoadPromptDialog : Window
    {
        public QuickLoadPromptAction UserAction { get; private set; } = QuickLoadPromptAction.Cancel;
        private readonly bool _isRebar;

        public QuickLoadPromptDialog(string familyName, bool isRebar = false)
        {
            InitializeComponent();
            _isRebar = isRebar;

            if (_isRebar)
            {
                TxtTitle.Text = "Rebar Shape Library Missing";
                TxtWarningNotice.Text = "No KhimTools Rebar Shapes (T00-T80) are loaded in the active project.";
                TxtDescription.Text = "The Rebar Engineering Engine requires the standard rebar shape library. Click below to load the complete Rebar library at once.";
                BtnPrimaryLoad.Content = "Load All Rebar Families";
            }
            else
            {
                TxtTitle.Text = $"Family '{familyName}' Not Loaded";
                TxtWarningNotice.Text = $"The structural family '{familyName}' was not found in the active project.";
                TxtDescription.Text = $"You can load only '{familyName}' directly to place it immediately, or open the Family Manager to review all available library families.";
                BtnPrimaryLoad.Content = $"Load '{familyName}' Only";
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnPrimaryLoad_Click(object sender, RoutedEventArgs e)
        {
            UserAction = _isRebar ? QuickLoadPromptAction.LoadAllRebar : QuickLoadPromptAction.LoadSingleFamily;
            DialogResult = true;
            Close();
        }

        private void BtnOpenManager_Click(object sender, RoutedEventArgs e)
        {
            UserAction = QuickLoadPromptAction.OpenFamilyManager;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            UserAction = QuickLoadPromptAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
