using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Models
{
    /// <summary>
    /// Ứng với 1 dòng trong bảng "Danh sách thép chủ (Cột tròn)".
    /// Binding trực tiếp vào DataGridView bên Form.
    /// </summary>
    public class MainBarRow
    {
        public string Story { get; set; }          // Tên tầng, VD: "Tầng 1"
        public double D { get; set; }               // Đường kính cột (mm, hiển thị)
        public double Hb { get; set; }               // Chiều cao đoạn cột (mm)
        public string Diameter { get; set; }         // Đường kính thép chủ, VD: "D20"
        public int Qty { get; set; }                 // Số lượng thanh
        public bool Dowel { get; set; }              // Có thép chờ (dowel) nối tầng dưới không
        public bool TopAnchor { get; set; }           // Có neo đầu cột không

        public ElementId ColumnId { get; set; }       // Id của cột trong model (ẩn, không hiện lên bảng)
    }
}
