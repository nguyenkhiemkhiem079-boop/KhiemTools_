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
        public bool SplitFoldersByFormat { get; set; } = true;
        public int PrintTimeoutSeconds { get; set; } = 120;
        public string IssueSetName { get; set; } = "Official Release";
        public DateTime IssueDate { get; set; } = DateTime.Today;
        public bool PreservePreviousExports { get; set; } = true;
        public string PreviousExportsFolderName { get; set; } = "Previous Exports";

        // PDF Specific Options & Appearance
        public bool CombinePdf { get; set; } = false;
        public string CombinedPdfFileName { get; set; } = "Combined_Sheets.pdf";
        public bool UseNamingConvention { get; set; } = true;
        public string NamingField1 { get; set; } = "";
        public string NamingField2 { get; set; } = "";
        public string NamingField3 { get; set; } = "";
        public bool AddBookmarks { get; set; } = true;
        public bool AutoCoverPage { get; set; } = false;
        public bool ApplyWatermark { get; set; } = false;
        public string WatermarkText { get; set; } = "IFC - ISSUED FOR CONSTRUCTION";

        // PDF Settings (Matching Sample Pro UI)
        public bool PaperPlacementCenter { get; set; } = false;
        public bool PaperPlacementOffset { get; set; } = true;
        public bool MarginNoMargin { get; set; } = true;
        public double MarginOffsetX { get; set; } = 0;
        public double MarginOffsetY { get; set; } = 0;
        public bool ZoomFitToPage { get; set; } = false;
        public int ZoomPercentage { get; set; } = 100;
        public bool VectorProcessing { get; set; } = true;
        public int PdfExportDpi { get; set; } = 300;
        public string RasterQuality { get; set; } = "Presentation";
        public string ColorMode { get; set; } = "Color";
        public bool ViewLinksInBlue { get; set; } = true;
        public bool HideRefPlanes { get; set; } = true;
        public bool HideUnreferencedViewTags { get; set; } = true;
        public bool HideScopeBoxes { get; set; } = true;
        public bool HideCropBoundaries { get; set; } = true;
        public bool ReplaceHalftoneWithThinLines { get; set; } = false;
        public bool MaskCoincidentLines { get; set; } = false;

        // Issue & Transmittal Options
        public bool GenerateTransmittal { get; set; } = true;
        public bool GenerateQaReport { get; set; } = true;

        // Reliability / QA
        public int MaxRetryCount { get; set; } = 2;
        public bool WarnPageSizeMismatch { get; set; } = true;
        public bool AutoDisableTemporaryViewProperties { get; set; } = true;
    }
}
