using System;

namespace KhimTools.MEP.Penetrations
{
    /// <summary>Geometry-only millimetre rectangle; never supplies a clearance or design default.</summary>
    public sealed class MepOpeningCutPlanV1
    {
        public double X0 { get; }
        public double X1 { get; }
        public double Y0 { get; }
        public double Y1 { get; }
        public double ClearanceMm { get; }
        public double MinimumWidthMm { get; }
        public double MinimumHeightMm { get; }
        public double OffsetXMm { get; }
        public double OffsetYMm { get; }
        private MepOpeningCutPlanV1(double x0,double x1,double y0,double y1,double clearance,double minimumWidth,double minimumHeight,double offsetX,double offsetY)
        {X0=x0;X1=x1;Y0=y0;Y1=y1;ClearanceMm=clearance;MinimumWidthMm=minimumWidth;MinimumHeightMm=minimumHeight;OffsetXMm=offsetX;OffsetYMm=offsetY;}
        // Compatibility adapter for existing QA resources: explicit zero means no added minimum/offset.
        public static MepOpeningCutPlanV1 Resolve(double x0,double x1,double y0,double y1,double width,double height,double? clearanceMm)
            =>Resolve(x0,x1,y0,y1,width,height,clearanceMm,0,0,0,0);
        public static MepOpeningCutPlanV1 Resolve(double x0,double x1,double y0,double y1,double width,double height,double? clearanceMm,
            double? minimumWidthMm,double? minimumHeightMm,double? offsetXMm,double? offsetYMm)
        {
            bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
            if(!clearanceMm.HasValue || !Finite(clearanceMm.Value) || clearanceMm.Value<0)
                throw new ArgumentException("Explicit finite non-negative per-side clearance in mm is required; no default.");
            foreach(double v in new[]{x0,x1,y0,y1,width,height})if(!Finite(v))throw new ArgumentException("Finite physical extents required.");
            if(x1<=x0 || y1<=y0 || width<=0 || height<=0)throw new ArgumentException("Positive physical extents required.");
            foreach(var value in new[]{minimumWidthMm,minimumHeightMm,offsetXMm,offsetYMm})
                if(!value.HasValue || !Finite(value.Value))throw new ArgumentException("Explicit finite minimum dimensions and offsets required; no default.");
            if(minimumWidthMm.Value<0 || minimumHeightMm.Value<0)throw new ArgumentException("Minimum dimensions must be non-negative; explicit zero means no added minimum.");
            double c=clearanceMm.Value;
            double cutWidth=Math.Max(x1-x0+2*c,minimumWidthMm.Value),cutHeight=Math.Max(y1-y0+2*c,minimumHeightMm.Value);
            double centerX=x0+(x1-x0)/2+offsetXMm.Value,centerY=y0+(y1-y0)/2+offsetYMm.Value;
            var plan=new MepOpeningCutPlanV1(centerX-cutWidth/2,centerX+cutWidth/2,centerY-cutHeight/2,centerY+cutHeight/2,c,
                minimumWidthMm.Value,minimumHeightMm.Value,offsetXMm.Value,offsetYMm.Value);
            foreach(double value in new[]{plan.X0,plan.X1,plan.Y0,plan.Y1})if(!Finite(value))throw new ArgumentException("Resolved rectangle overflow; no proposal.");
            // 1e-7 mm is numerical containment tolerance, not an engineering clearance allowance.
            if(plan.X0>x0-c+1e-7 || plan.X1<x1+c-1e-7 || plan.Y0>y0-c+1e-7 || plan.Y1<y1+c-1e-7)
                throw new ArgumentException("Offset would lose the full actual clash plus explicit per-side clearance; enlarge explicit minimum dimensions or remove offset.");
            if(plan.X0<=0 || plan.Y0<=0 || plan.X1>=width || plan.Y1>=height)
                throw new ArgumentException("Opening would reach/breach a host boundary; never clamp or infer structural edge clearance.");
            return plan;
        }
    }
}
