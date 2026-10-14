using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>Paint uses detached data only. No Document, Element, Solid or other Revit API references.</summary>
    internal sealed class MepOpeningPreviewFormV1 : Form
    {
        internal MepOpeningPreviewFormV1(MepOpeningPreviewGeometryV1 geometry,string summary)
        {
            Text="Review exact opening proposal — no model mutation";Size=new Size(1000,680);MinimumSize=new Size(720,480);
            StartPosition=FormStartPosition.CenterScreen;
            var pane=new PreviewCanvas(geometry){Dock=DockStyle.Fill,BackColor=Color.White};
            var information=new TextBox{Dock=DockStyle.Top,Height=155,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Text=summary};
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,AutoScroll=true};
            var twoD=new Button{Text="2D host plane",AutoSize=true};var threeD=new Button{Text="3D wireframe",AutoSize=true};
            var confirm=new Button{Text="Approve proposal",AutoSize=true,DialogResult=DialogResult.OK};
            var cancel=new Button{Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
            twoD.Click+=(s,e)=>{pane.Isometric=false;pane.Invalidate();};threeD.Click+=(s,e)=>{pane.Isometric=true;pane.Invalidate();};
            buttons.Controls.AddRange(new Control[]{twoD,threeD,confirm,cancel});
            Controls.Add(pane);Controls.Add(information);Controls.Add(buttons);CancelButton=cancel;
            // No AcceptButton: Enter cannot silently approve. Native confirmation still defaults to Cancel.
        }
        private sealed class PreviewCanvas : Control
        {
            private readonly MepOpeningPreviewGeometryV1 _geometry;
            internal bool Isometric=true;
            internal PreviewCanvas(MepOpeningPreviewGeometryV1 geometry){_geometry=geometry??throw new ArgumentNullException(nameof(geometry));DoubleBuffered=true;ResizeRedraw=true;}
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);if(ClientSize.Width<80||ClientSize.Height<80)return;
                var points=_geometry.Host.Concat(_geometry.Cut).Concat(_geometry.Penetrant).Select(p=>p.Project(Isometric)).ToArray();
                double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
                double scale=Math.Min((ClientSize.Width-60)/Math.Max(1,maxX-minX),(ClientSize.Height-70)/Math.Max(1,maxY-minY));
                PointF Screen(MepPreviewPointV1 p){p=p.Project(Isometric);return new PointF((float)(30+(p.X-minX)*scale),(float)(ClientSize.Height-30-(p.Y-minY)*scale));}
                void Box(System.Collections.Generic.IReadOnlyList<MepPreviewPointV1> box,Pen pen)
                {var edges=MepOpeningPreviewGeometryV1.EdgePairs;for(int i=0;i<edges.Count;i+=2)e.Graphics.DrawLine(pen,Screen(box[edges[i]]),Screen(box[edges[i+1]]));}
                using(var host=new Pen(Color.Gray,1))using(var cut=new Pen(Color.DarkOrange,2))using(var axis=new Pen(Color.Blue,2))
                {
                    Box(_geometry.Host,host);Box(_geometry.Cut,cut);
                    if(Isometric)e.Graphics.DrawLine(axis,Screen(_geometry.Penetrant[0]),Screen(_geometry.Penetrant[1]));
                    else {var point=Screen(_geometry.Penetrant[0]);e.Graphics.DrawEllipse(axis,point.X-4,point.Y-4,8,8);}
                }
                e.Graphics.DrawString("Grey: physical host | Orange: approved full-depth cut | Blue: actual MEP centerline only",Font,Brushes.Black,5,5);
            }
        }
    }
}
