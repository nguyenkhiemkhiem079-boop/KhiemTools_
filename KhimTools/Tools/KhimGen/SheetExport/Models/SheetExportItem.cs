using System;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace KhimTools.SheetExport.Models
{
    public enum SheetIssueStatus
    {
        New,          // Sheet chưa từng xuất trong snapshot cũ
        Modified,     // RevisionNumber khác với lần xuất trước
        Unchanged,    // Không đổi so với đợt trước
        Deleted       // Không còn trong Revit doc nhưng từng được xuất
    }

    public enum ExportFormat
    {
        PDF,
        DWG,
        NWC,
        IFC
    }

    public class SheetExportItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private string _computedFileName = "";
        private string _exportStatusText = "Sẵn sàng";
        private bool _isFailed;

        public ViewSheet Sheet { get; set; }
        public ElementId SheetId => Sheet?.Id;
        public string SheetUniqueId { get; set; } = "";
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public string CurrentRevisionNumber { get; set; } = "";
        public string CurrentRevisionDate { get; set; } = "";
        public string RevisionSequence { get; set; } = "";
        public string PaperSize { get; set; } = "A1";
        public string Orientation { get; set; } = "Landscape";
        public SheetIssueStatus IssueStatus { get; set; } = SheetIssueStatus.New;
        public string StatusBadgeText => IssueStatus switch
        {
            SheetIssueStatus.New => "🟢 Mới",
            SheetIssueStatus.Modified => "🟠 Đã sửa",
            SheetIssueStatus.Unchanged => "⚪ Không đổi",
            SheetIssueStatus.Deleted => "🔴 Đã xóa",
            _ => ""
        };

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string ComputedFileName
        {
            get => _computedFileName;
            set
            {
                if (_computedFileName != value)
                {
                    _computedFileName = value;
                    OnPropertyChanged(nameof(ComputedFileName));
                }
            }
        }

        public bool IsRegexValid { get; set; } = true;

        public string ExportStatusText
        {
            get => _exportStatusText;
            set
            {
                if (_exportStatusText != value)
                {
                    _exportStatusText = value;
                    OnPropertyChanged(nameof(ExportStatusText));
                }
            }
        }

        public bool IsFailed
        {
            get => _isFailed;
            set
            {
                if (_isFailed != value)
                {
                    _isFailed = value;
                    OnPropertyChanged(nameof(IsFailed));
                }
            }
        }

        public bool IsLocked { get; set; }
        public string LockedFilePath { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public double DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public int RetryCount { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
