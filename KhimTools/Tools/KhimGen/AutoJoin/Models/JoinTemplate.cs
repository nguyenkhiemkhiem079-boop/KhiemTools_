using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Template lưu cấu hình Join Elements (danh sách rules + scope mặc định).
    /// Serializable bằng JSON.
    /// </summary>
    public class JoinTemplate
    {
        public string Name { get; set; } = "Default";
        public ScopeMode DefaultScope { get; set; } = ScopeMode.CurrentView;
        public List<JoinTemplateRule> Rules { get; set; } = new List<JoinTemplateRule>();
    }

    /// <summary>
    /// Phiên bản serializable của CategoryMatchRule (lưu tên category thay vì enum int).
    /// </summary>
    public class JoinTemplateRule
    {
        public string CategoryA { get; set; } = "OST_Floors";
        public string CategoryB { get; set; } = "OST_Floors";
    }
}
