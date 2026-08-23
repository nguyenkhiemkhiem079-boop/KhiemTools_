namespace KhimTools.RebarTool.Models
{
    /// <summary>Dữ liệu nhập cho tab "Stirrups" — đai cột tròn.</summary>
    public class StirrupRow
    {
        public string Diameter { get; set; } = "D8";   // Ø đai, VD "D8"
        public double Spacing { get; set; } = 150;       // khoảng cách đai (mm)
        public int Legs { get; set; } = 1;                // số nhánh (1 = vòng đơn, cột tròn thường 1 vòng)
        public bool UseSpiral { get; set; } = false;      // true = đai xoắn liên tục, false = vòng rời
    }
}
