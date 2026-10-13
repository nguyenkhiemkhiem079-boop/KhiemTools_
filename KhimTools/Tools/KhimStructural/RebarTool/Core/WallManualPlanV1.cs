using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    public sealed class WallManualPointV1
    {
        public double? XMm { get; set; }
        public double? YMm { get; set; }
        public double? DepthMm { get; set; }
    }
    public sealed class WallManualBarV1
    {
        public string GroupId { get; set; }
        public string Role { get; set; }
        public BeamManualElementReferenceV1 BarType { get; set; }
        public WallManualPointV1 Start { get; set; }
        public WallManualPointV1 Finish { get; set; }
    }
    public sealed class WallManualCoversV1
    {
        public double? ExteriorMm { get; set; }
        public double? InteriorMm { get; set; }
        public double? LeftMm { get; set; }
        public double? RightMm { get; set; }
        public double? BottomMm { get; set; }
        public double? TopMm { get; set; }
        public double?[] Values()=>new[]{ExteriorMm,InteriorMm,LeftMm,RightMm,BottomMm,TopMm};
    }
    public sealed class WallBoundaryColumnV1
    {
        public string UniqueId { get; set; }
        public string DocumentKey { get; set; }
        public string GeometrySignature { get; set; }
        public double? ExposedClearCoverMm { get; set; }
        public string Intent { get; set; }
        public bool IsBound()=>!string.IsNullOrWhiteSpace(UniqueId) && !string.IsNullOrWhiteSpace(DocumentKey) &&
            !string.IsNullOrWhiteSpace(GeometrySignature) && !string.IsNullOrWhiteSpace(Intent) &&
            WallManualPlanValidatorV1.Finite(ExposedClearCoverMm) && ExposedClearCoverMm.Value>=0;
    }
    public sealed class WallManualPlanV1
    {
        public int SchemaVersion { get; set; } = 1;
        public string Mode { get; set; } = "WALL_MANUAL_V1";
        public string Units { get; set; } = "mm";
        public string DocumentKey { get; set; }
        public string HostUniqueId { get; set; }
        public string FrameSignature { get; set; }
        public WallManualCoversV1 Covers { get; set; } = new WallManualCoversV1();
        public List<WallManualBarV1> Bars { get; set; } = new List<WallManualBarV1>();
        public WallBoundaryColumnV1 LeftColumn { get; set; }
        public WallBoundaryColumnV1 RightColumn { get; set; }
        public bool CodeVerified { get; set; }
    }
    /// <summary>Explicit inputs and physical line-spacing checks only; real concrete/column solids remain an adapter gate.</summary>
    public static class WallManualPlanValidatorV1
    {
        public static bool Finite(double? value)=>value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        private static bool Valid(WallManualPointV1 p)=>p!=null && Finite(p.XMm) && Finite(p.YMm) && Finite(p.DepthMm);
        public static IReadOnlyList<string> Validate(WallManualPlanV1 plan)
        {
            var errors=new List<string>();
            if(plan==null){errors.Add("A Wall manual plan is required.");return errors;}
            if(plan.SchemaVersion!=1 || plan.Mode!="WALL_MANUAL_V1" || plan.Units!="mm" || plan.CodeVerified)
                errors.Add("Unsupported schema/mode/units or fabricated code-compliance claim.");
            if(string.IsNullOrWhiteSpace(plan.DocumentKey) || string.IsNullOrWhiteSpace(plan.HostUniqueId) || string.IsNullOrWhiteSpace(plan.FrameSignature))
                errors.Add("Exact Wall document/host/frame binding is required.");
            if(plan.Covers==null || plan.Covers.Values().Any(value=>!Finite(value) || value.Value<0))
                errors.Add("All six face covers must be explicitly finite and non-negative; null is not zero.");
            foreach(var column in new[]{plan.LeftColumn,plan.RightColumn}.Where(item=>item!=null))
                if(!column.IsBound() || column.DocumentKey!=plan.DocumentKey || column.UniqueId==plan.HostUniqueId)
                    errors.Add("Boundary column must be explicitly bound in the same document; Wall remains the Rebar host.");
            if(plan.LeftColumn!=null && plan.RightColumn!=null && plan.LeftColumn.UniqueId==plan.RightColumn.UniqueId)
                errors.Add("Distinct left/right boundary columns are required.");
            if(plan.Bars==null || plan.Bars.Count==0 || plan.Bars.Count>10000){errors.Add("Supply explicit physical bars (maximum10000 is software protection, not a design quantity).");return errors;}
            if(plan.Bars.Any(bar=>bar==null) || plan.Bars.Where(bar=>bar!=null).GroupBy(bar=>bar.GroupId,StringComparer.Ordinal).Any(group=>group.Count()>1))
                errors.Add("Every physical bar requires a distinct stable ID.");
            foreach(var bar in plan.Bars.Where(bar=>bar!=null))
            {
                if(string.IsNullOrWhiteSpace(bar.GroupId) || (bar.Role!="VERTICAL" && bar.Role!="HORIZONTAL" && bar.Role!="USER_DIRECTED"))errors.Add("Explicit bar ID and supported direction/role are required.");
                var type=bar.BarType;
                if(type==null || type.DocumentKey!=plan.DocumentKey || type.State!=BeamManualReferenceState.Resolved || string.IsNullOrWhiteSpace(type.UniqueId) ||
                    string.IsNullOrWhiteSpace(type.Name) || !Finite(type.DiameterMm) || type.DiameterMm<=0)errors.Add("Exact loaded bar identity and actual diameter are required; no substitution.");
                if(!Valid(bar.Start) || !Valid(bar.Finish)){errors.Add(bar.GroupId+": all six endpoint coordinates must be supplied in mm.");continue;}
                double dx=bar.Finish.XMm.Value-bar.Start.XMm.Value, dy=bar.Finish.YMm.Value-bar.Start.YMm.Value, dz=bar.Finish.DepthMm.Value-bar.Start.DepthMm.Value;
                if(dx*dx+dy*dy+dz*dz<=0.0001)errors.Add(bar.GroupId+": zero-length path.");
                if(bar.Role=="VERTICAL" && (Math.Abs(dx)>0.01 || Math.Abs(dz)>0.01))errors.Add(bar.GroupId+": VERTICAL role must retain explicit x/depth.");
                if(bar.Role=="HORIZONTAL" && (Math.Abs(dy)>0.01 || Math.Abs(dz)>0.01))errors.Add(bar.GroupId+": HORIZONTAL role must retain explicit height/depth.");
            }
            if(errors.Count==0)for(int i=0;i<plan.Bars.Count;i++)for(int j=i+1;j<plan.Bars.Count;j++)
            {
                var a=plan.Bars[i];var b=plan.Bars[j];double distance=(a.BarType.DiameterMm+b.BarType.DiameterMm)/2-0.01;
                if(distance>0 && BeamManualSegmentDistanceV1.SquaredDistance(Point(a.Start),Point(a.Finish),Point(b.Start),Point(b.Finish))<distance*distance)
                {
                    errors.Add("Physical bar spacing/collision between "+a.GroupId+" and "+b.GroupId+"; no design spacing is inferred.");
                    return errors; // Fail closed without allocating quadratic numbers of duplicate diagnostics.
                }
            }
            return errors;
        }
        private static BeamManualPoint3V1 Point(WallManualPointV1 p)=>new BeamManualPoint3V1(p.XMm.Value,p.YMm.Value,p.DepthMm.Value);
    }
}
