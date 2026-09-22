using System;
using System.Collections.Generic;
using System.IO;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class ExportHistoryService
    {
        public static string ArchiveExistingOutputs(IList<SheetExportItem> sheets, ExportOptions options)
        {
            if (sheets == null || sheets.Count == 0 || options == null || !options.PreservePreviousExports)
                return "";

            var normalized = ExportJobOptions.Normalize(options);
            return ArchiveExistingOutputs(sheets, normalized, new ExportPathResolver(normalized, DateTime.Now.ToString("yyyyMMddHHmmss")));
        }

        public static string ArchiveExistingOutputs(IList<SheetExportItem> sheets, ExportJobOptions options, ExportPathResolver paths)
        {
            if (sheets == null || sheets.Count == 0 || options == null || !options.PreservePreviousExports) return "";
            string archiveRoot = paths == null ? Path.Combine(options.OutputDirectory, options.PreviousExportsFolderName ?? "Previous Exports", DateTime.Now.ToString("yyyy-MM-dd_HHmmss")) : paths.ArchiveRoot;

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in sheets)
            {
                if (options.ExportPdf && !options.CombinePdf)
                    candidates.Add(paths == null ? Path.Combine(options.OutputDirectory, sheet.ComputedFileName + ".pdf") : paths.ResolveSheetPath(sheet, ExportFormat.PDF));
                if (options.ExportDwg)
                    candidates.Add(paths == null ? Path.Combine(options.OutputDirectory, sheet.ComputedFileName + ".dwg") : paths.ResolveSheetPath(sheet, ExportFormat.DWG));
            }

            if (options.ExportPdf && options.CombinePdf)
            {
                string combinedName = Path.GetFileNameWithoutExtension(options.CombinedPdfFileName);
                candidates.Add(paths == null ? Path.Combine(options.OutputDirectory, combinedName + ".pdf") : paths.ResolveCombinedPdfPath());
            }

            bool archivedAny = false;
            foreach (string source in candidates)
            {
                if (!File.Exists(source)) continue;
                if (PdfExportEngine.IsFileLocked(source))
                    throw new FileLockedException(source, $"Không thể lưu phiên bản cũ vì file '{Path.GetFileName(source)}' đang được mở.");

                Directory.CreateDirectory(archiveRoot);
                string target = Path.Combine(archiveRoot, Path.GetFileName(source));
                File.Move(source, GetUniquePath(target));
                archivedAny = true;
            }

            return archivedAny ? archiveRoot : "";
        }

        private static string GetUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string folder = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int version = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(folder, $"{name}_v{version}{extension}");
                version++;
            } while (File.Exists(candidate));
            return candidate;
        }
    }
}
