using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SectionCutTool.Core
{
    public class SectionCutResultItem
    {
        public Element Element { get; set; }
        public ViewSection CreatedView { get; set; }
        public string ViewName { get; set; }
        public bool IsLongitudinal { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Báo cáo chi tiết quá trình sinh mặt cắt tự động (Section Generation Report).
    /// </summary>
    public class SectionGenerationReport
    {
        public List<SectionCutResultItem> Items { get; } = new List<SectionCutResultItem>();

        public int TotalProcessed => Items.Count;
        public int SuccessCount { get; private set; } = 0;
        public int FailureCount { get; private set; } = 0;

        public void AddSuccess(Element elem, ViewSection view, bool isLongitudinal)
        {
            SuccessCount++;
            Items.Add(new SectionCutResultItem
            {
                Element = elem,
                CreatedView = view,
                ViewName = view?.Name ?? "",
                IsLongitudinal = isLongitudinal,
                Success = true
            });
        }

        public void AddError(Element elem, string viewName, bool isLongitudinal, Exception ex)
        {
            FailureCount++;
            Items.Add(new SectionCutResultItem
            {
                Element = elem,
                ViewName = viewName,
                IsLongitudinal = isLongitudinal,
                Success = false,
                ErrorMessage = ex?.Message ?? "Unknown Error"
            });
        }
    }
}
