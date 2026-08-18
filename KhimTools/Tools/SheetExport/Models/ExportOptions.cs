using System;

namespace KhimTools.SheetExport.Models
{
    public class ExportOptions
    {
        // Target Formats
        public bool ExportPdf { get; set; } = true;
        public bool ExportDwg { get; set; } = false;
        public string DwgExportSetupName { get; set; } = "In-Session Setup";

        // General Destination
        public string OutputDirectory { get; set; } = "";
        public string ProjectCode { get; set; } = "PROJ";
        public string SelectedNamingTemplateName { get; set; } = "Mặc định (SheetNumber - SheetName)";

        // PDF Specific Options
        public bool CombinePdf { get; set; } = false;
        public string CombinedPdfFileName { get; set; } = "Combined_Sheets.pdf";
        public bool AddBookmarks { get; set; } = true;
        public bool AutoCoverPage { get; set; } = false;
        public bool ApplyWatermark { get; set; } = false;
        public string WatermarkText { get; set; } = "IFC - ISSUED FOR CONSTRUCTION";

        // Issue & Transmittal Options
        public string IssueSetName { get; set; } = "Official Release";
        public bool GenerateTransmittal { get; set; } = true;
        public bool GenerateQaReport { get; set; } = true;

        // Reliability / QA
        public int MaxRetryCount { get; set; } = 2;
        public bool WarnPageSizeMismatch { get; set; } = true;
    }
}
