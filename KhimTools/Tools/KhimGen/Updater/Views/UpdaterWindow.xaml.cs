using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using KhimTools.Tools.Updater.Models;
using KhimTools.Tools.Updater.Services;

namespace KhimTools.Tools.Updater.Views
{
    public partial class UpdaterWindow : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;
        private bool _isCompleted = false;

        public UpdaterWindow(UpdateInfo updateInfo, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo ?? new UpdateInfo();
            _updateService = updateService ?? new UpdateService();

            DataContext = this;
            LoadInfo();
        }

        private void LoadInfo()
        {
            TxtCurrentVersion.Text = _updateInfo.CurrentVersion;
            TxtLatestVersion.Text = _updateInfo.LatestVersion;
            TxtReleaseDate.Text = " " + _updateInfo.ReleaseDate;
            ListChangelog.ItemsSource = _updateInfo.Changelog;
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

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            var msgResult = MessageBox.Show(
                "Để cập nhật an toàn và tránh xung đột khóa tập tin DLL, Autodesk Revit cần được đóng.\n\n" +
                "Bạn có muốn khởi chạy Trình cài đặt / Cập nhật K-TOOLS bên ngoài không?",
                "Cập nhật K-TOOLS",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (msgResult == MessageBoxResult.Yes)
            {
                _updateService.LaunchExternalUpdater();
                Close();
            }
        }
    }
}
