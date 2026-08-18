using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public static class HostedRebarQuery
    {
        /// <summary>Lấy toàn bộ Rebar element thật (không phải summary) đã gắn vào 1 cột.</summary>
        public static List<Rebar> GetHostedRebar(Document doc, Element column)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(r => r.GetHostId() == column.Id)
                .ToList();
        }
    }
}
