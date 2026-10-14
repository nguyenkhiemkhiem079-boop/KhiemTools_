using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    public sealed class ColumnWallRelatedCoversV1
    {
        public double? FrontMm {get;set;}
        public double? BackMm {get;set;}
        public double? LeftMm {get;set;}
        public double? RightMm {get;set;}
        public double? BottomMm {get;set;}
        public double? TopMm {get;set;}
        public double?[] Values()=>new[]{FrontMm,BackMm,LeftMm,RightMm,BottomMm,TopMm};
    }
    public sealed class ColumnWallRelatedPlanV1
    {
        public int SchemaVersion {get;set;}=1;
        public string Mode {get;set;}="COLUMN_HOST_WALL_RELATED_V1";
        public string Units {get;set;}="mm";
        public string DocumentKey {get;set;}
        public string HostColumnUniqueId {get;set;}
        public string FrameSignature {get;set;}
        public bool UserConfirmedActualColumnHost {get;set;}
        public string ScenarioIntent {get;set;}
        public bool CodeVerified {get;set;}
        public ColumnWallRelatedCoversV1 Covers {get;set;}=new ColumnWallRelatedCoversV1();
        // Shared geometric bar DTO, NOT a shared primary-host contract.
        public List<WallManualBarV1> Bars {get;set;}=new List<WallManualBarV1>();
        public static IReadOnlyList<string> Validate(ColumnWallRelatedPlanV1 plan)
        {
            var errors=new List<string>();
            bool Finite(double? value)=>WallManualPlanValidatorV1.Finite(value);
            if(plan==null){errors.Add("Column-host related plan required.");return errors;}
            if(plan.SchemaVersion!=1||plan.Mode!="COLUMN_HOST_WALL_RELATED_V1"||plan.Units!="mm"||plan.CodeVerified||!plan.UserConfirmedActualColumnHost)
                errors.Add("Explicit ContractB actual Column ownership confirmation required; no Wall/design substitution.");
            if(string.IsNullOrWhiteSpace(plan.DocumentKey)||string.IsNullOrWhiteSpace(plan.HostColumnUniqueId)||string.IsNullOrWhiteSpace(plan.FrameSignature)||string.IsNullOrWhiteSpace(plan.ScenarioIntent))
                errors.Add("Exact Column document/identity/frame and explicit wall-related scenario intent required.");
            if(plan.Covers==null||plan.Covers.Values().Any(v=>!Finite(v)||v.Value<0))errors.Add("All six actual Column face covers must be explicitly supplied.");
            if(plan.Bars==null||plan.Bars.Count==0||plan.Bars.Count>10000){errors.Add("Supply explicit physical bars; maximum10000 is software protection.");return errors;}
            if(plan.Bars.Any(b=>b==null)||plan.Bars.Where(b=>b!=null).GroupBy(b=>b.GroupId,StringComparer.Ordinal).Any(g=>g.Count()>1))errors.Add("Distinct explicit physical bar IDs required.");
            foreach(var bar in plan.Bars.Where(b=>b!=null)) {
                if(string.IsNullOrWhiteSpace(bar.GroupId)||(bar.Role!="VERTICAL"&&bar.Role!="HORIZONTAL"&&bar.Role!="USER_DIRECTED"))errors.Add("Explicit bar identity/direction required.");
                var type=bar.BarType;
                if(type==null||type.DocumentKey!=plan.DocumentKey||type.State!=BeamManualReferenceState.Resolved||string.IsNullOrWhiteSpace(type.UniqueId)||string.IsNullOrWhiteSpace(type.Name)||!Finite(type.DiameterMm)||type.DiameterMm<=0)errors.Add("Exact loaded bar identity/actual diameter required; no substitution.");
                var a=bar.Start;var b=bar.Finish;
                if(a==null||b==null||new[]{a.XMm,a.YMm,a.DepthMm,b.XMm,b.YMm,b.DepthMm}.Any(v=>!Finite(v))){errors.Add("All explicit Column-local endpoint coordinates required.");continue;}
                double dx=b.XMm.Value-a.XMm.Value,dy=b.YMm.Value-a.YMm.Value,dz=b.DepthMm.Value-a.DepthMm.Value,length=Math.Sqrt(dx*dx+dy*dy+dz*dz);
                if(length<=0.01)errors.Add("Zero-length Column path rejected.");
                if(bar.Role=="VERTICAL"&&(Math.Abs(dx)>0.01||Math.Abs(dz)>0.01)||bar.Role=="HORIZONTAL"&&(Math.Abs(dy)>0.01||Math.Abs(dz)>0.01))errors.Add("Explicit role must agree with actual Column-local direction.");
                var details=new[]{bar.StartAnchorageMm,bar.FinishAnchorageMm,bar.StartExtensionMm,bar.FinishExtensionMm};
                if(details.Any(v=>!Finite(v)||v.Value<0))errors.Add("Explicit terminal anchorage/embed and extension lengths required; no code calculation/default.");
                else {
                    if(bar.StartAnchorageMm+bar.FinishAnchorageMm>length+0.01)errors.Add("Declared terminal embeds exceed actual path.");
                    if(bar.StartExtensionMm!=0||bar.FinishExtensionMm!=0)errors.Add("ContractB v1 supports Column-contained paths only; external Wall extension needs a separately verified region contract, never a host switch.");
                }
            }
            if(errors.Count==0)for(int i=0;i<plan.Bars.Count;i++)for(int j=i+1;j<plan.Bars.Count;j++) {
                var a=plan.Bars[i];var b=plan.Bars[j];double d=(a.BarType.DiameterMm+b.BarType.DiameterMm)/2-0.01;
                BeamManualPoint3V1 P(WallManualPointV1 p)=>new BeamManualPoint3V1(p.XMm.Value,p.YMm.Value,p.DepthMm.Value);
                if(d>0&&BeamManualSegmentDistanceV1.SquaredDistance(P(a.Start),P(a.Finish),P(b.Start),P(b.Finish))<d*d){errors.Add("Physical Column path collision; no design spacing inferred.");return errors;}
            }
            return errors;
        }
    }
}
