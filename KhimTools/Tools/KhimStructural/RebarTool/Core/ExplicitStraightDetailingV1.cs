using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    internal static class ExplicitStraightDetailingV1
    {
        internal static void RequireKnownReinforcement(Document doc)
        {
            if(new FilteredElementCollector(doc).WhereElementIsNotElementType().Any(e=>e is RebarInSystem||e is FabricSheet||e is FabricArea||e is AreaReinforcement||e is PathReinforcement))
                throw new InvalidOperationException("Area/path/system/fabric reinforcement clearance is unsupported in these explicit contracts; no system bars may be silently ignored.");
        }
        // Software geometry contract: terminal anchorage means the user-declared
        // straight embedded terminal segment, NOT computed code development length.
        internal static void Verify(Line path,Solid primary,WallManualBarV1 input,Solid startRegion=null,Solid finishRegion=null)
        {
            var a=path.GetEndPoint(0);var b=path.GetEndPoint(1);var direction=(b-a).Normalize();
            using(var cut=primary.IntersectWithCurve(path,new SolidCurveIntersectionOptions()))
            {
                if(cut.SegmentCount!=1)throw new InvalidOperationException("A straight bar must actually intersect one primary-host interval; support-only bars are forbidden.");
                var segment=cut.GetCurveSegment(0);double p=(segment.GetEndPoint(0)-a).DotProduct(direction),q=(segment.GetEndPoint(1)-a).DotProduct(direction);
                double start=Math.Max(0,Math.Min(p,q)),finish=Math.Max(0,path.Length-Math.Max(p,q));
                if(Math.Abs(WallNativeFrameV1.Mm(start)-input.StartExtensionMm.Value)>0.01 || Math.Abs(WallNativeFrameV1.Mm(finish)-input.FinishExtensionMm.Value)>0.01)
                    throw new InvalidOperationException("Explicit extension lengths differ from actual outside-primary-host straight geometry; never clamp/infer them.");
                Embed(a,direction,input.StartAnchorageMm.Value,start>WallNativeFrameV1.Feet(0.01)?startRegion:primary);
                Embed(b,-direction,input.FinishAnchorageMm.Value,finish>WallNativeFrameV1.Feet(0.01)?finishRegion:primary);
            }
        }
        private static void Embed(XYZ terminal,XYZ direction,double declaredMm,Solid region)
        {
            if(region==null)throw new InvalidOperationException("Outside-host terminal needs the explicitly verified boundary region.");
            if(declaredMm==0)return; // Explicit user-entered zero, never supplied by production.
            var path=Line.CreateBound(terminal,terminal+direction*WallNativeFrameV1.Feet(declaredMm));
            using(var cut=region.IntersectWithCurve(path,new SolidCurveIntersectionOptions()))
                if(cut.SegmentCount!=1 || Math.Abs(cut.GetCurveSegment(0).Length-path.Length)>WallNativeFrameV1.Feet(0.01))
                    throw new InvalidOperationException("Declared straight terminal anchorage/embed segment is not entirely in its verified terminal host region.");
        }
    }
}
