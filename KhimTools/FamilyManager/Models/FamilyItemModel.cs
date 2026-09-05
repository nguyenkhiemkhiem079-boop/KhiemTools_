using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace KhimTools.FamilyManager.Models
{
    public class FamilyItemModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isLoadedInProject;
        private FamilyItemStatus _status = FamilyItemStatus.NotLoaded;
        private string _errorMessage;
        private int _loadedTypeCount;

        public string FamilyName { get; set; }
        public string CategoryName { get; set; } = "Generic";
        public string FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public int SourcePriority { get; set; } = 100;
        public DateTime LastModified { get; set; }
        public FamilyGroupType GroupType { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoadedInProject
        {
            get => _isLoadedInProject;
            set
            {
                if (_isLoadedInProject != value)
                {
                    _isLoadedInProject = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusBadge));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusColorHex));
                }
            }
        }

        public int LoadedTypeCount
        {
            get => _loadedTypeCount;
            set
            {
                if (_loadedTypeCount != value)
                {
                    _loadedTypeCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public FamilyItemStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(StatusColorHex));
                    OnPropertyChanged(nameof(StatusBadge));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        // ── Display Helpers ──────────────────────────────────────────────

        public string FileSizeText
        {
            get
            {
                if (FileSizeBytes <= 0) return "-";
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024:F0} KB";
                return $"{FileSizeBytes / (1024.0 * 1024):F1} MB";
            }
        }

        /// <summary>Short label for status badge in DataGrid.</summary>
        public string StatusBadge
        {
            get
            {
                switch (_status)
                {
                    case FamilyItemStatus.Loaded: return "IN PROJECT";
                    case FamilyItemStatus.UpToDate: return "UP TO DATE";
                    case FamilyItemStatus.UpdateAvailable: return "UPDATE AVAIL";
                    case FamilyItemStatus.NotLoaded: return "NOT LOADED";
                    case FamilyItemStatus.LoadFailed: return "LOAD FAILED";
                    case FamilyItemStatus.ReloadFailed: return "RELOAD FAIL";
                    case FamilyItemStatus.NotFound: return "NOT FOUND";
                    default: return _status.ToString().ToUpper();
                }
            }
        }

        /// <summary>WPF Brush color for the status badge background.</summary>
        public Brush StatusColor
        {
            get
            {
                switch (_status)
                {
                    case FamilyItemStatus.Loaded:
                    case FamilyItemStatus.UpToDate:
                        return new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)); // Green
                    case FamilyItemStatus.UpdateAvailable:
                        return new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)); // Amber
                    case FamilyItemStatus.LoadFailed:
                    case FamilyItemStatus.ReloadFailed:
                        return new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)); // Red
                    case FamilyItemStatus.NotFound:
                        return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)); // Muted
                    default: // NotLoaded
                        return new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)); // Slate
                }
            }
        }

        /// <summary>Hex string color for older binding compatibility.</summary>
        public string StatusColorHex
        {
            get
            {
                switch (_status)
                {
                    case FamilyItemStatus.UpToDate:
                    case FamilyItemStatus.Loaded:
                        return "#16A34A";
                    case FamilyItemStatus.NotLoaded:
                        return "#64748B";
                    case FamilyItemStatus.UpdateAvailable:
                        return "#D97706";
                    case FamilyItemStatus.LoadFailed:
                    case FamilyItemStatus.ReloadFailed:
                        return "#DC2626";
                    case FamilyItemStatus.NotFound:
                        return "#94A3B8";
                    default:
                        return "#475569";
                }
            }
        }

        public string StatusDisplay
        {
            get
            {
                switch (_status)
                {
                    case FamilyItemStatus.UpToDate: return "Loaded (Up to Date)";
                    case FamilyItemStatus.Loaded: return "Loaded";
                    case FamilyItemStatus.NotLoaded: return "Not Loaded";
                    case FamilyItemStatus.UpdateAvailable: return "Update Available";
                    case FamilyItemStatus.LoadFailed: return "Load Failed";
                    case FamilyItemStatus.ReloadFailed: return "Reload Failed";
                    case FamilyItemStatus.NotFound: return "File Not Found";
                    default: return _status.ToString();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
