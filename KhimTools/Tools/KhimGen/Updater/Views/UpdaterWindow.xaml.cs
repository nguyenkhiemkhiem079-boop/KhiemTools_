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

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompleted)
            {
                Close();
                return;
            }

            BtnAction.IsEnabled = false;
            BtnAction.Content = "Đang tải xuống...";
            ProgressBarDownload.Visibility = Visibility.Visible;
            ProgressBarDownload.IsIndeterminate = true;

            var progress = new Progress<double>(val =>
            {
                ProgressBarDownload.IsIndeterminate = false;
                ProgressBarDownload.Value = val;
            });

            bool success = await _updateService.DownloadAndStageUpdateAsync(_updateInfo.DownloadUrl, progress);

            ProgressBarDownload.Visibility = Visibility.Collapsed;

            if (success)
            {
                _isCompleted = true;
                PanelStatus.Visibility = Visibility.Visible;
                BtnAction.IsEnabled = true;
                BtnAction.Content = "Đóng";
                BtnAction.Style = (Style)FindResource("ModernButton");
            }
            else
            {
                BtnAction.IsEnabled = true;
                BtnAction.Content = "Thử lại";
                MessageBox.Show("Không thể tải bản cập nhật. Vui lòng kiểm tra kết nối mạng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
