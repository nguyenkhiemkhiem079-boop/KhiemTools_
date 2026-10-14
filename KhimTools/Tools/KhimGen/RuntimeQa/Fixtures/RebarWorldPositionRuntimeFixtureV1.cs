using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class RebarWorldPositionRuntimeFixtureV1 : RuntimeQaFixtureBase
    {
        public override string Id=>"REBAR-WORLD-POSITION-PREVIEW";
        public override string Name=>"Distributed/moved actual Rebar world-position preview";
        public override string Suite=>"REBAR";
        public override bool IsCritical=>true;
        public override string Description=>"Read-only actual existing multi-position set versus native final-position centerlines. Missing dispersed resources BLOCKED. Does not certify moved/varying/excluded cases without those actual resources.";
        protected override void ExecuteFixture(RuntimeQaContext context,QaFixtureResult result)
        {
            var doc=context.Document;var before=RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            Rebar set=null;List<RebarPreviewPath> expected=null;
            foreach(var bar in new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>())
            {
                if(bar.NumberOfBarPositions<2)continue;
                var paths=new List<RebarPreviewPath>();var firstPoints=new List<XYZ>();
                for(int i=0;i<bar.NumberOfBarPositions;i++) {
                    if(!bar.DoesBarExistAtPosition(i))continue;
                    var curves=bar.GetTransformedCenterlineCurves(false,false,false,MultiplanarOption.IncludeAllMultiplanarCurves,i);
                    if(curves.Count==0)throw new InvalidOperationException("Native final-position resource missing.");
                    firstPoints.Add(curves[0].GetEndPoint(0));
                    foreach(var curve in curves)paths.Add(new RebarPreviewPath(curve.Tessellate().Select(p=>new RebarPreviewPoint(p.X,p.Y,p.Z))));
                }
                if(firstPoints.Count>=2&&firstPoints.Skip(1).Any(p=>p.DistanceTo(firstPoints[0])>1e-5)){set=bar;expected=paths;break;}
            }
            if(set==null){Block(result,Id+"_RESOURCES","Actual dispersed set","At least two included final-position bars","BLOCKED: no real dispersed Rebar set; no synthetic host PASS.",QaSeverity.CRITICAL);return;}
            var actual=RebarPreviewService.ReadPaths(new List<Rebar>{set});
            bool positions=actual.Count==expected.Count&&RebarPreviewService.Fingerprint(actual)==RebarPreviewService.Fingerprint(expected);
            Check(result,Id+"_POSITION","Canonical reader versus native final positions",positions,"All included bars at native final transformed/moved locations",positions.ToString(),"Separate raw/base-position API must not be substituted.",QaSeverity.CRITICAL);
            bool clean=RuntimeQaSafetyGuard.VerifyRollback(doc,before,Array.Empty<ElementId>(),out string message);
            Check(result,Id+"_READONLY","Model identities/state unchanged",clean,"Read-only fixture with no new elements",message,"Additional shifted/varying/excluded resources remain explicit host matrix cases.",QaSeverity.CRITICAL);
        }
    }
}
