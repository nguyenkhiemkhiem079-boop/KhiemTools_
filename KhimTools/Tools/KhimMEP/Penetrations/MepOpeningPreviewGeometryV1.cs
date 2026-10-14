using System;
using System.Collections.Generic;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>Detached host-local millimetres. Isometric projection is a wireframe, not a perspective/rendered solid.</summary>
    public readonly struct MepPreviewPointV1
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public MepPreviewPointV1(double x,double y,double z)
        {
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)||double.IsNaN(z)||double.IsInfinity(z))
                throw new ArgumentException("Finite detached preview coordinates required.");
            X=x;Y=y;Z=z;
        }
        public MepPreviewPointV1 Project(bool isometric)=>isometric
            ?new MepPreviewPointV1((X-Z)/Math.Sqrt(2),(X+Z)/Math.Sqrt(6)+Y*Math.Sqrt(2.0/3),0)
            :new MepPreviewPointV1(X,Y,0);
    }
    public sealed class MepOpeningPreviewGeometryV1
    {
        public IReadOnlyList<MepPreviewPointV1> Host { get; }
        public IReadOnlyList<MepPreviewPointV1> Cut { get; }
        public IReadOnlyList<MepPreviewPointV1> Penetrant { get; }
        public static readonly IReadOnlyList<int> EdgePairs=Array.AsReadOnly(new[]{0,1,1,3,3,2,2,0,4,5,5,7,7,6,6,4,0,4,1,5,2,6,3,7});
        public MepOpeningPreviewGeometryV1(double width,double height,double thickness,MepOpeningCutPlanV1 plan,
            MepPreviewPointV1 start,MepPreviewPointV1 finish)
        {
            if(plan==null || !(width>0)||!(height>0)||!(thickness>0) || plan.X0<=0||plan.Y0<=0||plan.X1>=width||plan.Y1>=height)
                throw new ArgumentException("Verified host and interior cut required for detached preview.");
            Host=Box(0,width,0,height,thickness);Cut=Box(plan.X0,plan.X1,plan.Y0,plan.Y1,thickness);
            Penetrant=Array.AsReadOnly(new[]{start,finish});
        }
        private static IReadOnlyList<MepPreviewPointV1> Box(double x0,double x1,double y0,double y1,double depth)
            =>Array.AsReadOnly(new[]{new MepPreviewPointV1(x0,y0,0),new MepPreviewPointV1(x1,y0,0),new MepPreviewPointV1(x0,y1,0),new MepPreviewPointV1(x1,y1,0),
                new MepPreviewPointV1(x0,y0,depth),new MepPreviewPointV1(x1,y0,depth),new MepPreviewPointV1(x0,y1,depth),new MepPreviewPointV1(x1,y1,depth)});
    }
}
