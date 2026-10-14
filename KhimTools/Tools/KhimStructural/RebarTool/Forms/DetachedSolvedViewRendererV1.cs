using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>Drawing only: detached numeric primitives, no native model access.</summary>
    internal static class DetachedSolvedViewRendererV1
    {
        internal static void Paint(Graphics g,Rectangle bounds,DetachedSolvedViewV1 view)
        {
            g.Clear(Color.White);if(view==null || bounds.Width<80 || bounds.Height<80)return;
            using(var font=new Font("Segoe UI",8f))
            {
                g.DrawString("ACCEPTED SOLVER CENTERLINES · "+view.HostLabel+" · host-only outline; symbolic intersections, NOT bar diameter",font,Brushes.DarkSlateGray,new RectangleF(6,3,bounds.Width-12,36));
                int half=bounds.Width/2;
                Draw(g,new Rectangle(6,62,Math.Max(1,half-12),Math.Max(1,bounds.Height-70)),view.Section,
                    view.SectionX0,view.SectionZ0,view.SectionWidth,view.SectionHeight,view.CircularSection);
                Draw(g,new Rectangle(half+6,62,Math.Max(1,bounds.Width-half-12),Math.Max(1,bounds.Height-70)),view.Elevation,
                    view.ElevationX0,view.ElevationY0,view.ElevationWidth,view.ElevationHeight,false);
                g.DrawString("TRUE POLYLINE CUT · "+(view.CutFeet*304.8).ToString("0.###")+" mm",font,Brushes.Black,6,42);
                g.DrawString("SOLVED ELEVATION · all layers projected",font,Brushes.Black,half+6,42);
            }
        }
        private static void Draw(Graphics g,Rectangle viewport,IReadOnlyList<SlabSectionPrimitive> paths,double x0,double y0,double w,double h,bool circle)
        {
            if(!(w>0&&h>0))return;
            double xmin=x0,ymin=y0,xmax=x0+w,ymax=y0+h;
            foreach(var p in paths){xmin=Math.Min(xmin,Math.Min(p.StartAlong,p.EndAlong));xmax=Math.Max(xmax,Math.Max(p.StartAlong,p.EndAlong));ymin=Math.Min(ymin,Math.Min(p.StartElevation,p.EndElevation));ymax=Math.Max(ymax,Math.Max(p.StartElevation,p.EndElevation));}
            double scale=Math.Min(Math.Max(1,viewport.Width-12)/(xmax-xmin),Math.Max(1,viewport.Height-12)/(ymax-ymin));
            float dx=(float)(viewport.Left+(viewport.Width-(xmax-xmin)*scale)/2),dy=(float)(viewport.Top+(viewport.Height-(ymax-ymin)*scale)/2);
            PointF P(double x,double y)=>new PointF(dx+(float)((x-xmin)*scale),dy+(float)((ymax-y)*scale));
            var old=g.Save();try {
                g.SetClip(viewport);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var a=P(x0,y0+h);var b=P(x0+w,y0);var host=new RectangleF(a.X,a.Y,b.X-a.X,b.Y-a.Y);
                using(var outline=new Pen(Color.Gray,1f)){if(circle)g.DrawEllipse(outline,host);else g.DrawRectangle(outline,host.X,host.Y,host.Width,host.Height);}
                foreach(var path in paths) {
                    var color=RebarSolverPreviewForm.RoleColor(path.Role);var start=P(path.StartAlong,path.StartElevation);
                    if(path.Kind==SlabSectionPrimitiveKind.IntersectionMarker)using(var brush=new SolidBrush(color))g.FillEllipse(brush,start.X-2,start.Y-2,4,4);
                    else using(var pen=new Pen(color,1.5f))g.DrawLine(pen,start,P(path.EndAlong,path.EndElevation));
                }
                if(paths.Count==0)using(var font=new Font("Segoe UI",8f))g.DrawString("No centerline intersects this cut",font,Brushes.Gray,new RectangleF(viewport.Left+3,viewport.Top+3,viewport.Width-6,50));
            }finally{g.Restore(old);}
        }
    }
}
