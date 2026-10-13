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
        private MepOpeningCutPlanV1(double x0,double x1,double y0,double y1,double clearance)
        {X0=x0;X1=x1;Y0=y0;Y1=y1;ClearanceMm=clearance;}
        public static MepOpeningCutPlanV1 Resolve(double x0,double x1,double y0,double y1,double width,double height,double? clearanceMm)
        {
            bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
            if(!clearanceMm.HasValue || !Finite(clearanceMm.Value) || clearanceMm.Value<0)
                throw new ArgumentException("Explicit finite non-negative per-side clearance in mm is required; no default.");
            foreach(double v in new[]{x0,x1,y0,y1,width,height})if(!Finite(v))throw new ArgumentException("Finite physical extents required.");
            if(x1<=x0 || y1<=y0 || width<=0 || height<=0)throw new ArgumentException("Positive physical extents required.");
            double c=clearanceMm.Value;
            var plan=new MepOpeningCutPlanV1(x0-c,x1+c,y0-c,y1+c,c);
            if(plan.X0<=0 || plan.Y0<=0 || plan.X1>=width || plan.Y1>=height)
                throw new ArgumentException("Opening would reach/breach a host boundary; never clamp or infer structural edge clearance.");
            return plan;
        }
    }
}
