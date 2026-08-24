using System;

namespace KhimTools.SheetExport.Models
{
    public class QaReportEntry
    {
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public string Format { get; set; } = "";
        public string OutputFilePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public double DurationSeconds { get; set; }
        public bool Success { get; set; }
        public bool IsLocked { get; set; }
        public int Retries { get; set; }
        public string Message { get; set; } = "";
    }
}
