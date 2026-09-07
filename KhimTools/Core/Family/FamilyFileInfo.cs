using System;
using System.IO;

namespace KhimTools.Core.Family
{
    /// <summary>
    /// Thông tin metadata của file Family (.rfa) được quản lý trong bộ thư viện K-TOOLS.
    /// </summary>
    public class FamilyFileInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Category { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsLoadedInDocument { get; set; }
        public int SymbolCount { get; set; }

        public FamilyFileInfo()
        {
        }

        public FamilyFileInfo(string filePath, string category = null)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            FullPath = filePath;
            Name = Path.GetFileNameWithoutExtension(filePath);
            Category = category ?? "Standard";

            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);
                FileSizeBytes = fi.Length;
                LastModified = fi.LastWriteTime;
            }
        }

        public string FormattedSize
        {
            get
            {
                if (FileSizeBytes < 1024) return FileSizeBytes + " B";
                if (FileSizeBytes < 1024 * 1024) return (FileSizeBytes / 1024.0).ToString("0.#") + " KB";
                return (FileSizeBytes / (1024.0 * 1024.0)).ToString("0.##") + " MB";
            }
        }
    }
}
