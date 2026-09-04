using System;
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
            TxtLatestVersion.Text = !string.IsNullOrEmpty(_updateInfo.LatestVersion) ? _updateInfo.LatestVersion : "N/A";
            TxtReleaseDate.Text = !string.IsNullOrEmpty(_updateInfo.ReleaseDate) ? _updateInfo.ReleaseDate : DateTime.Now.ToString("yyyy-MM-dd");
            TxtCommit.Text = !string.IsNullOrEmpty(_updateInfo.GitCommit) ? _updateInfo.GitCommit : "Release";
            ListChangelog.ItemsSource = _updateInfo.Changelog;

            switch (_updateInfo.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    TxtHeaderSubtitle.Text = "Có bản cập nhật thương mại mới! 🔔";
                    BtnDownload.Visibility = Visibility.Visible;
                    PanelRestartActions.Visibility = Visibility.Collapsed;
                    BtnCloseOnly.Visibility = Visibility.Collapsed;
                    break;

                case UpdateCheckStatus.UpToDate:
                    TxtHeaderSubtitle.Text = "Bạn đang sử dụng phiên bản mới nhất! ✓";
                    PanelStatusMessage.Visibility = Visibility.Visible;
                    TxtStatusDetail.Text = "Hệ thống K-TOOLS của bạn hoàn toàn cập nhật. Không có phiên bản mới hơn.";
                    BtnDownload.Visibility = Visibility.Collapsed;
                    PanelRestartActions.Visibility = Visibility.Collapsed;
                    BtnCloseOnly.Visibility = Visibility.Visible;
                    break;

                case UpdateCheckStatus.ServerUnavailable:
                    TxtHeaderSubtitle.Text = "Không thể kết nối máy chủ cập nhật ⚠️";
                    PanelStatusMessage.Visibility = Visibility.Visible;
                    TxtStatusDetail.Text = "Không thể liên hệ máy chủ GitHub để truy vấn manifest. Vui lòng kiểm tra Internet.";
                    BtnDownload.Visibility = Visibility.Collapsed;
                    PanelRestartActions.Visibility = Visibility.Collapsed;
                    BtnCloseOnly.Visibility = Visibility.Visible;
                    break;

                case UpdateCheckStatus.ReadyToInstall:
                    TxtHeaderSubtitle.Text = "Bản cập nhật đã sẵn sàng cài đặt! ✓";
                    PanelVerificationChecklist.Visibility = Visibility.Visible;
                    BtnDownload.Visibility = Visibility.Collapsed;
                    PanelRestartActions.Visibility = Visibility.Visible;
                    BtnCloseOnly.Visibility = Visibility.Collapsed;
                    break;

                default:
                    TxtHeaderSubtitle.Text = _updateInfo.StatusMessage;
                    BtnDownload.Visibility = Visibility.Collapsed;
                    PanelRestartActions.Visibility = Visibility.Collapsed;
                    BtnCloseOnly.Visibility = Visibility.Visible;
                    break;
            }
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

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            BtnDownload.IsEnabled = false;
            BtnDownload.Content = "Đang tải xuống & kiểm tra SHA256...";
            ProgressBarDownload.Visibility = Visibility.Visible;
            ProgressBarDownload.IsIndeterminate = false;

            var progress = new Progress<double>(val =>
            {
                ProgressBarDownload.Value = val;
            });

            bool success = await _updateService.DownloadAndStageUpdateAsync(_updateInfo, progress);
            ProgressBarDownload.Visibility = Visibility.Collapsed;

            if (success && _updateInfo.Status == UpdateCheckStatus.ReadyToInstall)
            {
                TxtHeaderSubtitle.Text = "Gói cập nhật đã được xác thực an toàn! ✓";
                BtnDownload.Visibility = Visibility.Collapsed;
                PanelVerificationChecklist.Visibility = Visibility.Visible;
                PanelRestartActions.Visibility = Visibility.Visible;
            }
            else
            {
                BtnDownload.IsEnabled = true;
                BtnDownload.Content = "Thử lại";
                PanelStatusMessage.Visibility = Visibility.Visible;
                TxtStatusDetail.Text = _updateInfo.StatusMessage;
                MessageBox.Show($"Xác thực cập nhật không thành công:\n{_updateInfo.StatusMessage}", 
                    "K-TOOLS Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnRestartUpdate_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Để áp dụng cập nhật an toàn mà không làm hỏng dữ liệu dự án:\n\n" +
                "1. Vui lòng lưu các mô hình Revit đang mở.\n" +
                "2. K-TOOLS Updater sẽ tự động tạo bản sao lưu (Backup) và cài đặt phiên bản mới ngay sau khi Revit đóng.\n" +
                "3. Revit sẽ tự động khởi động lại sau khi hoàn tất.\n\n" +
                "Bạn có muốn tiến hành Cập nhật ngay bây giờ?",
                "Xác nhận Cập nhật & Khởi động lại",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            bool launched = _updateService.LaunchExternalUpdater(_updateInfo);
            if (launched)
            {
                MessageBox.Show(
                    "Tiến trình KToolsUpdater.exe đã được khởi chạy độc lập!\n" +
                    "Vui lòng đóng Revit để tiến trình cập nhật tự động hoàn tất.",
                    "K-TOOLS Updater Active",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Close();
            }
            else
            {
                MessageBox.Show(
                    "Không thể khởi chạy KToolsUpdater.exe ngoài Revit. Vui lòng kiểm tra lại quyền truy cập thư mục cài đặt.",
                    "Lỗi khởi chạy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            // User chose "Later". Absolute rule: DO NOT touch current installation!
            Close();
        }
    }
}
