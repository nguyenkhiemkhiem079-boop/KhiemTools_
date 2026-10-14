using System.Drawing;
using System.Windows.Forms;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>Controls-only dialog; resolving/validating a chosen support belongs to the command callback.</summary>
    internal sealed class BeamSupportBindingFormV1 : Form
    {
        internal ComboBox SupportPicker { get; } = new ComboBox { Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList };
        internal TextBox CoverInput { get; } = new TextBox { Dock=DockStyle.Fill, AccessibleName="Explicit support cover (mm)" };
        internal TextBox IntentInput { get; } = new TextBox { Dock=DockStyle.Fill, AccessibleName="Explicit continuation intent" };
        internal Button BindButton { get; } = new Button { Text="Verify and bind / Xác minh", AutoSize=true };
        internal BeamSupportBindingFormV1(string end, bool columnsOnly=false)
        {
            Text="Bind "+end+" support / Chọn gối — geometry only";
            Size=new Size(720,380); MinimumSize=new Size(650,360);
            AutoScaleMode=AutoScaleMode.Dpi; AutoScroll=true; StartPosition=FormStartPosition.CenterParent;
            MaximizeBox=false; MinimizeBox=false;
            var layout=new TableLayoutPanel { Dock=DockStyle.Top, AutoSize=true, ColumnCount=1, RowCount=7, Padding=new Padding(10) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<7;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label TextLabel(string text)=>new Label { Text=text,Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(600,0),Margin=new Padding(3,8,3,3) };
            layout.Controls.Add(TextLabel(columnsOnly ? "Exact boundary Column in same document; Wall remains Rebar host / Cột biên; host vẫn là Wall" : "Exact same-document rectangular structural Column/Beam support / Gối chữ nhật cùng model"));
            layout.Controls.Add(SupportPicker);
            layout.Controls.Add(TextLabel("Explicit uniform clear cover on exposed support faces (mm) / Lớp bảo vệ gối; no default")); layout.Controls.Add(CoverInput);
            layout.Controls.Add(TextLabel("Explicit continuation/return intent; development and code compliance NOT verified / Ý định kéo dài")); layout.Controls.Add(IntentInput);
            layout.Controls.Add(BindButton); Controls.Add(layout);
        }
        private static Form CreateLayoutPreview()=>new BeamSupportBindingFormV1("LAYOUT ONLY");
    }
}
