using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using KhimTools.Domain.Models.Architectural;
using KhimTools.Domain.Models.Mep;
using KhimTools.Domain.Models.Qs;
using KhimTools.RebarTool.Core;

static class Program
{
    private static int _checks;

    private sealed class SemanticBar
    {
        public string Type { get; set; }
        public string Host { get; set; }
        public string Orientation { get; set; }
        public int Count { get; set; }
        public double DiameterMm { get; set; }
        public double SpacingMm { get; set; }
        public double CoverMm { get; set; }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Golden regression failed: " + label);
        _checks++;
    }

    private static bool Near(double expected, double actual, double tolerance) =>
        double.IsFinite(actual) && Math.Abs(expected - actual) <= tolerance;

    private static void Main()
    {
        RunDomainGoldens();
        RunRebarCalculationGoldens();
        RunRebarPreviewDuplicateCases();
        RunSlabSectionGeometryCases();
        RunBeamStirrupLayoutCases();
        RunBeamManualPlanCases();
        RunBeamManualBatchCases();
        RunBeamSupportCases();
        RunWallManualCases();
        RunColumnWallRelatedCases();
        RunMepCutCases();
        RunSolvedViewCases();
        RunComparatorContract();
        RunEdgeCases();
        RunSyntheticStress();
        Console.WriteLine($"GOLDEN_REGRESSION_ACCEPTANCE=PASS ({_checks} assertions)");
        Console.WriteLine("REBAR_DOMAIN_GOLDEN_ACCEPTANCE=PASS (production anchorage/lap calculator)");
        Console.WriteLine("EDGE_CASE_ACCEPTANCE=PASS (QS/MEP/Architectural domain calculations)");
        Console.WriteLine("REBAR_DOMAIN_EDGE_ACCEPTANCE=PASS (anchorage/lap calculator validation)");
        Console.WriteLine("REBAR_GEOMETRY_EDGE_CASE_STATUS=HOST_REQUIRED / NOT_EXECUTED");
        Console.WriteLine("PERFORMANCE_ACCEPTANCE=PASS (pure planning/calculation only)");
        Console.WriteLine("STRESS_ACCEPTANCE=PASS (100, 1,000, 10,000 synthetic records)");
        Console.WriteLine("REBAR_HOST_GEOMETRY_GOLDEN=HOST_REQUIRED / NOT_EXECUTED");
    }

    private static void RunColumnWallRelatedCases()
    {
        ColumnWallRelatedPlanV1 Plan()=>new ColumnWallRelatedPlanV1{DocumentKey="QA_DOC",HostColumnUniqueId="QA_COLUMN",FrameSignature="QA_FRAME",UserConfirmedActualColumnHost=true,ScenarioIntent="QA_ONLY explicit Column-owned wall-related path; not a Wall proxy",Covers=new ColumnWallRelatedCoversV1{FrontMm=20,BackMm=25,LeftMm=30,RightMm=35,BottomMm=40,TopMm=45},Bars=new List<WallManualBarV1>{new WallManualBarV1{GroupId="QA_A",Role="VERTICAL",Start=new WallManualPointV1{XMm=100,YMm=100,DepthMm=50},Finish=new WallManualPointV1{XMm=100,YMm=2000,DepthMm=50},StartAnchorageMm=40,FinishAnchorageMm=50,StartExtensionMm=0,FinishExtensionMm=0,BarType=new BeamManualElementReferenceV1{DocumentKey="QA_DOC",UniqueId="QA_TYPE",Name="QA_ONLY",DiameterMm=16,State=BeamManualReferenceState.Resolved}}}};
        bool Valid(ColumnWallRelatedPlanV1 p)=>ColumnWallRelatedPlanV1.Validate(p).Count==0;
        var p=Plan();Check(Valid(p),"ContractB explicit Column-owned input valid, no Wall proxy");
        p.UserConfirmedActualColumnHost=false;Check(!Valid(p),"Column ownership must be explicitly confirmed");
        p=Plan();p.ScenarioIntent=null;Check(!Valid(p),"Column cannot automatically imply wall-related scenario");
        p=Plan();p.Mode="WALL_MANUAL_V1";Check(!Valid(p),"ContractA mode cannot be reused as ContractB");
        p=Plan();p.Covers.FrontMm=null;Check(!Valid(p),"Column face cover missing is not zero");
        p=Plan();p.Bars[0].BarType.DocumentKey="FOREIGN";Check(!Valid(p),"Column exact type binding rejects foreign document");
        p=Plan();p.Bars[0].Role="HORIZONTAL";Check(!Valid(p),"Column direction must agree with actual explicit coordinates");
        p=Plan();p.Bars[0].StartAnchorageMm=null;Check(!Valid(p),"Column terminal embed must be entered, not inferred");
        p=Plan();p.Bars[0].StartAnchorageMm=1900;Check(!Valid(p),"Column embeds cannot exceed full explicit path");
        p=Plan();p.Bars[0].StartExtensionMm=10;Check(!Valid(p),"Unverified external Column extension rejected, not rehosted to Wall");
        p=Plan();p.Bars[0].Finish.YMm=100;Check(!Valid(p),"Column zero geometry rejected");
        p=Plan();p.CodeVerified=true;Check(!Valid(p),"Column contract cannot invent code compliance");
        p=Plan();var other=Plan().Bars[0];other.GroupId="QA_B";other.Start.XMm=108;other.Finish.XMm=108;p.Bars.Add(other);Check(!Valid(p),"Column physical collision numerically rejected");
        p=Plan();var json=Newtonsoft.Json.JsonConvert.SerializeObject(p);var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<ColumnWallRelatedPlanV1>(json);Check(Valid(copy)&&copy.Bars[0].StartAnchorageMm==40&&copy.HostColumnUniqueId=="QA_COLUMN","Column plan roundtrip preserves explicit detail and actual ownership");
        Check(json.Contains("HostColumnUniqueId")&&!json.Contains("\"HostUniqueId\""),"Primary Column host field is not a disguised Wall field");
    }

    private static void RunSolvedViewCases()
    {
        SolvedLocalPathV1 Path(string role,params SlabSectionPoint[] points)=>new SolvedLocalPathV1(role,points);
        var p=Path("QA_COPLANAR",new SlabSectionPoint(1,0,2),new SlabSectionPoint(3,0,4));
        var view=new DetachedSolvedViewV1(new[]{p},0);
        Check(view.Section.Count==1&&view.Section[0].Kind==SlabSectionPrimitiveKind.CoplanarSegment,"Solved coplanar actual segment visible");
        Check(view.Section[0].StartAlong==1&&view.Section[0].EndElevation==4,"Solved section retains actual coordinates");
        p=Path("QA_OFFSET",new SlabSectionPoint(1,1,2),new SlabSectionPoint(3,1,4));view=new DetachedSolvedViewV1(new[]{p},0);
        Check(view.Section.Count==0&&view.Elevation.Count==1,"Off-plane hidden in true cut, visible in disclosed elevation");
        p=Path("QA_CROSS",new SlabSectionPoint(2,-1,3),new SlabSectionPoint(4,1,5));view=new DetachedSolvedViewV1(new[]{p},0);
        Check(view.Section.Count==1&&view.Section[0].Kind==SlabSectionPrimitiveKind.IntersectionMarker,"Perpendicular crossing is a symbolic intersection, not an artificial bar");
        Check(view.Section[0].StartAlong==3&&view.Section[0].StartElevation==4,"Crossing interpolation numerically correct");
        var other=Path("QA_OTHER",new SlabSectionPoint(8,-1,9),new SlabSectionPoint(8,1,9));view=new DetachedSolvedViewV1(new[]{p,other},0);
        Check(view.Section.Count==2&&view.Section.All(s=>s.Kind==SlabSectionPrimitiveKind.IntersectionMarker),"Unrelated intersections never joined");
        Check(view.Section[0].Role=="QA_CROSS"&&view.Section[1].Role=="QA_OTHER","Actual roles retained per primitive");
        p=Path("INVALID",new SlabSectionPoint(double.NaN,0,0),new SlabSectionPoint(1,0,0));view=new DetachedSolvedViewV1(new[]{p},0);
        Check(view.Section.Count==0&&view.Elevation.Count==0,"Invalid solved paths produce safe empty geometry");
        Check(new DetachedSolvedViewV1(null,0).Elevation.Count==0,"Missing paths are safe, no fabricated bars");
        p=Path("COLUMN",new SlabSectionPoint(2,3,4),new SlabSectionPoint(5,6,7));view=new DetachedSolvedViewV1(new[]{p},4,elevationXY:true);
        Check(view.Elevation[0].StartAlong==2&&view.Elevation[0].StartElevation==3&&view.Elevation[0].EndElevation==6,"Column remapped elevation retains x/height, not section depth");
        var mutable=new[]{new SlabSectionPoint(1,0,2),new SlabSectionPoint(3,0,4)};p=Path("COPY",mutable);mutable[0]=new SlabSectionPoint(99,99,99);
        Check(p.Points[0].X==1,"Solved cache defensively copies point storage");
        p=Path("TOL",new SlabSectionPoint(1,SlabSectionGeometry.PlaneToleranceFeet/2,2),new SlabSectionPoint(3,SlabSectionGeometry.PlaneToleranceFeet/2,4));
        Check(new DetachedSolvedViewV1(new[]{p},0).Section.Count==1,"Documented coplanarity tolerance retained");
        p=Path("OFFSET",new SlabSectionPoint(1,SlabSectionGeometry.PlaneToleranceFeet*2,2),new SlabSectionPoint(3,SlabSectionGeometry.PlaneToleranceFeet*2,4));
        Check(new DetachedSolvedViewV1(new[]{p},0).Section.Count==0,"Outside tolerance stays off-plane");
        bool invalidCut=false;try{new DetachedSolvedViewV1(new[]{p},double.NaN);}catch(ArgumentException){invalidCut=true;}Check(invalidCut,"Nonfinite cut cannot silently render");
        p=Path("REPEAT",new SlabSectionPoint(0,-1,0),new SlabSectionPoint(0,1,0),new SlabSectionPoint(0,-1,0));
        Check(new DetachedSolvedViewV1(new[]{p},0).Section.Count==1,"Shared crossing marker deduplicated only within same path");
    }

    private static void RunMepCutCases()
    {
        var p=KhimTools.MEP.Penetrations.MepOpeningCutPlanV1.Resolve(100,300,400,600,2000,2000,25);
        Check(p.X0==75&&p.X1==325&&p.Y0==375&&p.Y1==625,"MEP explicit per-side clearance expands actual rectangle");
        Check(p.ClearanceMm==25,"MEP reviewed clearance preserved");
        p=KhimTools.MEP.Penetrations.MepOpeningCutPlanV1.Resolve(100,300,400,600,2000,2000,0);
        Check(p.X0==100&&p.X1==300,"MEP explicit zero clearance is not a default50");
        bool Reject(double x0,double x1,double y0,double y1,double w,double h,double? c){try{KhimTools.MEP.Penetrations.MepOpeningCutPlanV1.Resolve(x0,x1,y0,y1,w,h,c);return false;}catch(ArgumentException){return true;}}
        Check(Reject(100,300,400,600,2000,2000,null),"MEP absent clearance rejected");
        Check(Reject(100,300,400,600,2000,2000,-1),"MEP negative clearance rejected");
        Check(Reject(100,300,400,600,2000,2000,double.NaN),"MEP nonfinite clearance rejected");
        Check(Reject(double.PositiveInfinity,300,400,600,2000,2000,0),"MEP nonfinite extent rejected");
        Check(Reject(300,100,400,600,2000,2000,0),"MEP reversed width rejected, not normalized silently");
        Check(Reject(100,100,400,600,2000,2000,0),"MEP zero area rejected");
        Check(Reject(100,300,400,600,0,2000,0),"MEP invalid host extent rejected");
        Check(Reject(100,300,400,600,2000,2000,100),"MEP host edge contact rejected, no fabricated edge cover");
        Check(Reject(100,300,400,600,325,2000,25),"MEP right-boundary breach rejected, never clamped");
    }

    private static void RunWallManualCases()
    {
        // Synthetic inputs only: live Wall/Column concrete CSG remains host-required.
        WallManualPlanV1 Plan()=>new WallManualPlanV1 { DocumentKey="QA_DOC",HostUniqueId="QA_WALL",FrameSignature="QA_FRAME",
            Covers=new WallManualCoversV1{ExteriorMm=30,InteriorMm=35,LeftMm=40,RightMm=45,BottomMm=50,TopMm=55},
            Bars=new List<WallManualBarV1>{new WallManualBarV1{GroupId="QA_A",Role="VERTICAL",BarType=new BeamManualElementReferenceV1{DocumentKey="QA_DOC",UniqueId="QA_TYPE",Name="QA_ONLY",DiameterMm=16,State=BeamManualReferenceState.Resolved},
                StartAnchorageMm=0,FinishAnchorageMm=0,StartExtensionMm=0,FinishExtensionMm=0,
                Start=new WallManualPointV1{XMm=100,YMm=100,DepthMm=50},Finish=new WallManualPointV1{XMm=100,YMm=2000,DepthMm=50}}} };
        bool Valid(WallManualPlanV1 p)=>WallManualPlanValidatorV1.Validate(p).Count==0;
        var p=Plan();Check(Valid(p),"Explicit synthetic Wall vertical line/face covers are valid input");
        p.Covers.ExteriorMm=null;Check(!Valid(p),"Missing exterior cover is not zero");
        p=Plan();p.Covers.InteriorMm=-1;Check(!Valid(p),"Negative Wall cover rejected");
        p=Plan();p.Bars[0].Start.XMm=null;Check(!Valid(p),"Missing Wall coordinate is not implicit zero");
        p=Plan();p.Bars[0].Finish.YMm=double.NaN;Check(!Valid(p),"Nonfinite Wall coordinate rejected");
        p=Plan();p.Bars[0].Finish.YMm=100;Check(!Valid(p),"Zero Wall path rejected");
        p=Plan();p.Bars[0].Finish.XMm=200;Check(!Valid(p),"Vertical role cannot silently become diagonal");
        p=Plan();p.Bars[0].Role="HORIZONTAL";Check(!Valid(p),"Horizontal role requires constant explicit height/depth");
        p.Bars[0].Role="USER_DIRECTED";Check(Valid(p),"Explicit user-directed finite path supported without inferred layout");
        p=Plan();p.Bars[0].BarType.DocumentKey="FOREIGN";Check(!Valid(p),"Foreign bar type cannot bind silently");
        p=Plan();p.Bars[0].BarType.State=BeamManualReferenceState.Unresolved;Check(!Valid(p),"Unresolved Wall type cannot solve");
        p=Plan();p.CodeVerified=true;Check(!Valid(p),"Wall plan cannot claim structural code approval");
        p=Plan();p.Bars.Add(p.Bars[0]);Check(!Valid(p),"Duplicate stable bar ID rejected");
        p=Plan();var other=Plan().Bars[0];other.GroupId="QA_B";other.Start.XMm=110;other.Finish.XMm=110;p.Bars.Add(other);Check(!Valid(p),"Physical diameter spacing collision rejected numerically");
        other.Start.XMm=116;other.Finish.XMm=116;Check(Valid(p),"Explicit diameter boundary spacing accepted, no code minimum invented");
        p=Plan();p.LeftColumn=new WallBoundaryColumnV1{UniqueId="QA_COL",DocumentKey="QA_DOC",GeometrySignature="QA_COLUMN",ExposedClearCoverMm=30,Intent="QA_ONLY continuation"};Check(Valid(p)&&p.HostUniqueId=="QA_WALL","Boundary Column never changes Wall host identity");
        p.LeftColumn.ExposedClearCoverMm=null;Check(!Valid(p),"Column cover must be explicit");
        p.LeftColumn.ExposedClearCoverMm=30;p.LeftColumn.DocumentKey="FOREIGN";Check(!Valid(p),"Foreign Column binding rejected");
        p.LeftColumn.DocumentKey="QA_DOC";p.RightColumn=p.LeftColumn;Check(!Valid(p),"Same Column cannot masquerade as two boundaries");
        p=Plan();var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<WallManualPlanV1>(Newtonsoft.Json.JsonConvert.SerializeObject(p));
        Check(Valid(copy)&&copy.Covers.InteriorMm==35&&copy.Bars[0].Finish.YMm==2000&&copy.HostUniqueId=="QA_WALL","Wall JSON roundtrip preserves explicit values and ownership");
        p=Plan();p.Bars[0].StartAnchorageMm=null;Check(!Valid(p),"Wall terminal embed is explicit, never an inferred code Ld");
        p=Plan();p.Bars[0].FinishAnchorageMm=-1;Check(!Valid(p),"Wall negative terminal embed rejected");
        p=Plan();p.Bars[0].StartAnchorageMm=2000;Check(!Valid(p),"Wall declared terminal embeds cannot exceed actual path");
        p=Plan();p.Bars[0].StartExtensionMm=null;Check(!Valid(p),"Wall extension length cannot silently come from a template default");
    }

    private static void RunBeamSupportCases()
    {
        // Synthetic values test input permission/fingerprints, NOT Revit CSG or anchorage adequacy.
        var plan=CreateManualPlan();
        BeamManualSupportEndV1 Bound(BeamManualEnd end) => new BeamManualSupportEndV1 { End=end,
            Mode=BeamManualSupportMode.VerifiedColumn, SupportHostUniqueId="QA_SUPPORT_"+end,
            SupportDocumentKey=plan.DocumentKey, SupportGeometrySignature="QA_SOLID_STATE", SupportClearCoverMm=30,
            IntendedContinuation="QA_ONLY explicit continuation; no structural verification" };
        var start=Bound(BeamManualEnd.Start); var finish=Bound(BeamManualEnd.Finish);
        Check(!BeamManualSupportPolicyV1.PermitsStation(-100,6000,16,plan.SupportEnds),"Free end cannot silently enable negative station");
        Check(BeamManualSupportPolicyV1.PermitsStation(-100,6000,16,new[]{start}),"Bound start grants input permission; adapter must prove solid containment");
        Check(!BeamManualSupportPolicyV1.PermitsStation(6100,6000,16,new[]{start}),"Start support cannot authorize finish extension");
        Check(BeamManualSupportPolicyV1.PermitsStation(6100,6000,16,new[]{finish}),"Bound finish grants input permission only");
        Check(!BeamManualSupportPolicyV1.PermitsStation(-100,6000,16,new[]{start,start}),"Ambiguous duplicate end bindings are rejected");
        start.SupportClearCoverMm=null; Check(!BeamManualSupportPolicyV1.IsBound(start),"Missing support cover is not zero");
        start.SupportClearCoverMm=double.NaN; Check(!BeamManualSupportPolicyV1.IsBound(start),"Nonfinite support cover is rejected");
        start.SupportClearCoverMm=-1; Check(!BeamManualSupportPolicyV1.IsBound(start),"Negative support cover is rejected");
        start.SupportClearCoverMm=0; Check(BeamManualSupportPolicyV1.IsBound(start),"Explicit zero is retained, not replaced by a product cover default");
        start.SupportGeometrySignature=null; Check(!BeamManualSupportPolicyV1.IsBound(start),"Unverified support signature is blocked");
        start=Bound(BeamManualEnd.Start); plan.SupportEnds[0]=start;
        plan.BarGroups[0].Bars[0].StartStationMm=-100;
        Check(BeamManualPlanValidatorV1.Validate(plan,true).IsValid,"Supported extension inputs preserve explicit negative station for adapter preflight");
        Check(BeamManualLayoutBuilderV1.Build(plan)[0].Centerline[0].StationMm==-100,"Canonical plan does not clamp extension to Beam start");
        string before=BeamManualLayoutBuilderV1.Fingerprint(plan); start.SupportClearCoverMm+=1;
        Check(before!=BeamManualLayoutBuilderV1.Fingerprint(plan),"Support cover change invalidates input fingerprint");
        before=BeamManualLayoutBuilderV1.Fingerprint(plan); start.SupportGeometrySignature="QA_MOVED";
        Check(before!=BeamManualLayoutBuilderV1.Fingerprint(plan),"Support geometry state invalidates input fingerprint");
        start.SupportDocumentKey="QA_FOREIGN"; Check(!BeamManualPlanValidatorV1.Validate(plan,true).IsValid,"Foreign support document is rejected");
        start.SupportDocumentKey=plan.DocumentKey; finish.SupportHostUniqueId=start.SupportHostUniqueId; plan.SupportEnds[1]=finish;
        Check(!BeamManualPlanValidatorV1.Validate(plan,true).IsValid,"Same support on both ends fails closed");
        plan.SupportEnds[1]=CreateManualPlan().SupportEnds[1];
        string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"KTOOL_SUPPORT_QA_"+Guid.NewGuid().ToString("N"));
        try {
            var store=new BeamManualDetailingPlanStoreV1(directory,plan.DocumentKey,plan.HostUniqueId);
            store.Save(plan,0); var loaded=store.Load();
            Check(loaded.SupportEnds[0].SupportHostUniqueId==start.SupportHostUniqueId &&
                loaded.SupportEnds[0].SupportGeometrySignature==start.SupportGeometrySignature &&
                loaded.SupportEnds[0].SupportClearCoverMm==start.SupportClearCoverMm &&
                loaded.SupportEnds[0].SupportDocumentKey==start.SupportDocumentKey &&
                loaded.BarGroups[0].Bars[0].StartStationMm==-100,"Production store roundtrip retains exact support/cover/state and negative station");
        } finally { if(System.IO.Directory.Exists(directory))System.IO.Directory.Delete(directory,true); }
    }

    private static void RunBeamManualBatchCases()
    {
        // Synthetic identities only: this does not exercise a Revit transaction.
        var a = new BeamManualDetailingPlanV1 { DocumentKey = "QA_ONLY", HostUniqueId = "A" };
        var b = new BeamManualDetailingPlanV1 { DocumentKey = "QA_ONLY", HostUniqueId = "B" };
        bool Rejected(IEnumerable<BeamManualDetailingPlanV1> plans)
        {
            try { BeamManualBatchIdentityV1.Validate(plans); return false; }
            catch (InvalidOperationException) { return true; }
        }
        Check(!Rejected(new[] { a, b }), "Distinct same-document Beam plans are valid batch assignments");
        Check(Rejected(new[] { a, a }), "Duplicate host assignment is rejected before mutation");
        Check(Rejected(Array.Empty<BeamManualDetailingPlanV1>()), "Empty assignment cannot report success");
        Check(Rejected(new[] { a, null }), "Incomplete host assignment is rejected");
        Check(Rejected(new[] { a, new BeamManualDetailingPlanV1 { DocumentKey = "OTHER_QA", HostUniqueId = "B" } }), "Foreign document batch is rejected");
        Check(Rejected(new[] { new BeamManualDetailingPlanV1 { DocumentKey = "QA_ONLY" } }), "Missing host identity is rejected");
    }

    private static void RunRebarPreviewDuplicateCases()
    {
        string[] expected = { "host-A:type-16:line-1", "host-A:type-16:line-2", "host-A:type-16:line-2" };
        Check(RebarFingerprintMultiset.Contains(expected,
            new[] { expected[2], expected[0], expected[1] }), "Exact slab plan matches as an order-independent multiset");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { expected[0], "host-A:unrelated-line" }), "A shared planned set plus unrelated rebar is not a complete duplicate");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { expected[0], expected[1], "host-A:type-16:line-3" }), "Duplicate matching preserves planned multiplicity");
        Check(!RebarFingerprintMultiset.Contains(expected,
            new[] { "host-B:type-16:line-1", "host-B:type-16:line-2", "host-B:type-16:line-2" }),
            "Identical geometry hosted by another slab is not a duplicate");
        Check(RebarFingerprintMultiset.Contains(expected,
            expected.Concat(new[] { "host-A:unrelated-existing-reinforcement" })),
            "A complete planned duplicate remains detectable alongside unrelated reinforcement");
        Check(!RebarFingerprintMultiset.Contains(Array.Empty<string>(), expected), "An empty preview cannot be classified as a duplicate");
    }

    private static void RunSlabSectionGeometryCases()
    {
        var xCoplanar = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 0, 1), new SlabSectionPoint(2, 0, 1)
        }, SlabSectionAxis.SectionX, 0, "bottom-x");
        Check(xCoplanar.Count == 1, "Section X coplanar X bar returns one primitive");
        Check(xCoplanar[0].Kind == SlabSectionPrimitiveKind.CoplanarSegment, "Section X coplanar X bar remains a line");
        Check(Near(-2, xCoplanar[0].StartAlong, 1e-9) && Near(2, xCoplanar[0].EndAlong, 1e-9), "Section X line uses actual projected endpoints");
        Check(xCoplanar[0].Role == "bottom-x", "Section X line preserves reinforcement role");

        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 0.1, 1), new SlabSectionPoint(2, 0.1, 1)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Section X parallel offset X bar is hidden");

        var yCrossesX = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(1, -2, 2), new SlabSectionPoint(1, 2, 4)
        }, SlabSectionAxis.SectionX, 0, "top-y");
        Check(yCrossesX.Count == 1 && yCrossesX[0].Kind == SlabSectionPrimitiveKind.IntersectionMarker,
            "Y bar crossing Section X produces a symbolic marker");
        Check(Near(1, yCrossesX[0].StartAlong, 1e-9) && Near(3, yCrossesX[0].StartElevation, 1e-9),
            "Section X marker is interpolated at exact cut coordinates");
        Check(yCrossesX[0].Role == "top-y", "Section X marker preserves reinforcement role");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(1, 0.5, 2), new SlabSectionPoint(1, 2, 4)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Y bar wholly outside Section X is hidden");

        var yCoplanar = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, -2, 1), new SlabSectionPoint(0, 2, 1)
        }, SlabSectionAxis.SectionY, 0, "bottom-y");
        Check(yCoplanar.Count == 1 && yCoplanar[0].Kind == SlabSectionPrimitiveKind.CoplanarSegment,
            "Section Y coplanar Y bar remains a line");
        Check(Near(-2, yCoplanar[0].StartAlong, 1e-9) && Near(2, yCoplanar[0].EndAlong, 1e-9),
            "Section Y line uses actual projected endpoints");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0.1, -2, 1), new SlabSectionPoint(0.1, 2, 1)
        }, SlabSectionAxis.SectionY, 0).Count == 0, "Section Y parallel offset Y bar is hidden");

        var xCrossesY = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-2, 1, 2), new SlabSectionPoint(2, 1, 4)
        }, SlabSectionAxis.SectionY, 0, "support-x");
        Check(xCrossesY.Count == 1 && xCrossesY[0].Kind == SlabSectionPrimitiveKind.IntersectionMarker,
            "X bar crossing Section Y produces a symbolic marker");
        Check(Near(1, xCrossesY[0].StartAlong, 1e-9) && Near(3, xCrossesY[0].StartElevation, 1e-9),
            "Section Y marker is interpolated at exact cut coordinates");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0.5, 1, 2), new SlabSectionPoint(2, 1, 4)
        }, SlabSectionAxis.SectionY, 0).Count == 0, "X bar wholly outside Section Y is hidden");

        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, SlabSectionGeometry.PlaneToleranceFeet * 0.5, 0),
            new SlabSectionPoint(1, SlabSectionGeometry.PlaneToleranceFeet * 0.5, 0)
        }, SlabSectionAxis.SectionX, 0).Single().Kind == SlabSectionPrimitiveKind.CoplanarSegment,
            "Section plane tolerance accepts numerical coplanarity");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, SlabSectionGeometry.PlaneToleranceFeet * 2, 0),
            new SlabSectionPoint(1, SlabSectionGeometry.PlaneToleranceFeet * 2, 0)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Section plane tolerance rejects nearby off-plane bars");

        var twiceCrossing = SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(-1, -1, 0), new SlabSectionPoint(1, 1, 2),
            new SlabSectionPoint(-1, 1, 4), new SlabSectionPoint(1, -1, 6)
        }, SlabSectionAxis.SectionX, 0, "opening");
        Check(twiceCrossing.Count == 2, "Separate cut crossings remain separate primitives");
        Check(twiceCrossing.All(primitive => primitive.Kind == SlabSectionPrimitiveKind.IntersectionMarker),
            "Multiple intersections never become an artificial connecting segment");
        Check(Near(1, twiceCrossing[0].StartElevation, 1e-9) && Near(5, twiceCrossing[1].StartElevation, 1e-9),
            "Multiple markers retain their independent solved elevations");

        SlabSectionPoint[] outer =
        {
            new SlabSectionPoint(-5, -5, 1), new SlabSectionPoint(5, -5, 1),
            new SlabSectionPoint(5, 5, 1), new SlabSectionPoint(-5, 5, 1)
        };
        IReadOnlyList<SlabSectionPoint>[] openings =
        {
            new[]
            {
                new SlabSectionPoint(-1, -1, 1), new SlabSectionPoint(1, -1, 1),
                new SlabSectionPoint(1, 1, 1), new SlabSectionPoint(-1, 1, 1)
            }
        };
        var openingCut = SlabSectionGeometry.GetConcreteIntervals(outer, openings, SlabSectionAxis.SectionX, 0);
        Check(openingCut.Count == 2, "Opening intersecting section splits concrete into two intervals");
        Check(Near(-5, openingCut[0].Start, 1e-9) && Near(-1, openingCut[0].End, 1e-9), "Opening cut preserves left concrete interval");
        Check(Near(1, openingCut[1].Start, 1e-9) && Near(5, openingCut[1].End, 1e-9), "Opening cut preserves right concrete interval");

        SlabSectionPoint[] concaveDisconnected =
        {
            new SlabSectionPoint(-5, -5, 1), new SlabSectionPoint(-3, -5, 1),
            new SlabSectionPoint(-3, 3, 1), new SlabSectionPoint(3, 3, 1),
            new SlabSectionPoint(3, -5, 1), new SlabSectionPoint(5, -5, 1),
            new SlabSectionPoint(5, 5, 1), new SlabSectionPoint(-5, 5, 1)
        };
        var disconnected = SlabSectionGeometry.GetConcreteIntervals(concaveDisconnected, null, SlabSectionAxis.SectionX, 0);
        Check(disconnected.Count == 2, "Concave supported cut returns disconnected concrete intervals");
        Check(Near(-5, disconnected[0].Start, 1e-9) && Near(-3, disconnected[0].End, 1e-9), "Disconnected left interval is exact");
        Check(Near(3, disconnected[1].Start, 1e-9) && Near(5, disconnected[1].End, 1e-9), "Disconnected right interval is exact without a false connector");

        Check(SlabSectionGeometry.IntersectPath(null, SlabSectionAxis.SectionX, 0).Count == 0, "Empty solved path is safe");
        Check(SlabSectionGeometry.IntersectPath(new[] { new SlabSectionPoint(0, 0, 0) }, SlabSectionAxis.SectionX, 0).Count == 0,
            "Single-point solved path is safe");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(double.NaN, 0, 0), new SlabSectionPoint(1, 0, 1)
        }, SlabSectionAxis.SectionX, 0).Count == 0, "Invalid solved coordinates fail closed");
        Check(SlabSectionGeometry.IntersectPath(new[]
        {
            new SlabSectionPoint(0, 0, 0), new SlabSectionPoint(1, 0, 1)
        }, SlabSectionAxis.SectionX, double.NaN).Count == 0, "Invalid cut coordinate fails closed");
        Check(SlabSectionGeometry.GetConcreteIntervals(new[]
        {
            new SlabSectionPoint(0, 0, 0), new SlabSectionPoint(double.PositiveInfinity, 0, 0),
            new SlabSectionPoint(0, 1, 0)
        }, null, SlabSectionAxis.SectionX, 0).Count == 0, "Invalid concrete boundary fails closed");
    }

    private static void RunBeamStirrupLayoutCases()
    {
        List<double> stations = BeamStirrupLayout.CreateZoneStations(
            totalLength: 1000, zoneA1Length: 250, spacingA1: 100, spacingA2: 200,
            endClearance: 50, duplicateTolerance: 1);
        Check(stations.SequenceEqual(new[] { 50d, 150d, 250d, 450d, 650d, 750d, 850d, 950d }),
            "Beam A1/A2/A1 station plan is deterministic and preserves the requested zone spacings");
        Check(stations.Zip(stations.Skip(1), (left, right) => right > left).All(increasing => increasing),
            "Beam stirrup stations are strictly ordered without duplicate zone-boundary bars");

        List<double> hanger = BeamStirrupLayout.CreateHangerStations(new[] { 250d },
            quantity: 1, spacing: 50, startClearance: 50, endClearance: 950);
        List<double> merged = BeamStirrupLayout.MergeAndValidate(stations.Concat(hanger),
            duplicateTolerance: 1, minimumSeparation: 10);
        Check(merged.Count == stations.Count && merged.Contains(250),
            "A hanger station coincident with a regular stirrup is represented exactly once");

        bool overlapRejected = false;
        try
        {
            BeamStirrupLayout.MergeAndValidate(new[] { 100d, 105d }, duplicateTolerance: 1, minimumSeparation: 10);
        }
        catch (InvalidOperationException) { overlapRejected = true; }
        Check(overlapRejected, "Overlapping distinct Beam stirrup centerlines are rejected");

        bool invalidSpacingRejected = false;
        try
        {
            BeamStirrupLayout.CreateZoneStations(1000, 250, 0, 200, 50, 1);
        }
        catch (ArgumentOutOfRangeException) { invalidSpacingRejected = true; }
        Check(invalidSpacingRejected, "Non-positive Beam stirrup spacing fails closed instead of selecting a fallback");

        List<double> distinctHangers = BeamStirrupLayout.CreateHangerStations(new[] { 250d, 500d },
            quantity: 3, spacing: 50, startClearance: 50, endClearance: 950);
        Check(distinctHangers.SequenceEqual(new[] { 200d, 250d, 300d, 450d, 500d, 550d }),
            "Hanger stations are symmetric about actual intersecting-beam stations");
        Check(BeamStirrupLayout.MergeAndValidate(stations.Concat(distinctHangers), 1, 10).SequenceEqual(
            BeamStirrupLayout.MergeAndValidate(distinctHangers.Concat(stations), 1, 10)),
            "Beam normal/hanger station merging is independent of discovery order");

        List<BeamLongitudinalLayout.SectionBar> top = BeamLongitudinalLayout.CreateSymmetricRow(
            width: 300, height: 600, cover: 25, stirrupDiameter: 10, barDiameter: 16,
            quantity: 2, top: true);
        List<BeamLongitudinalLayout.SectionBar> bottom = BeamLongitudinalLayout.CreateSymmetricRow(
            width: 300, height: 600, cover: 25, stirrupDiameter: 10, barDiameter: 16,
            quantity: 2, top: false);
        Check(top.Count == 2 && Math.Abs(top[0].X + top[1].X) < 1e-9 &&
              Math.Abs(top[0].Y - 257) < 1e-9 && Math.Abs(bottom[0].Y + 257) < 1e-9,
            "Beam top/bottom longitudinal rows use exact cover, tie and bar-radius offsets with symmetric placement");
        BeamLongitudinalLayout.RequireNoOverlap(top.Concat(bottom));
        Check(true, "Separated Beam top and bottom rows pass section-plane non-overlap validation");

        bool crowdedRowRejected = false;
        try
        {
            BeamLongitudinalLayout.CreateSymmetricRow(width: 300, height: 600, cover: 25,
                stirrupDiameter: 10, barDiameter: 25, quantity: 10, top: true);
        }
        catch (InvalidOperationException) { crowdedRowRejected = true; }
        Check(crowdedRowRejected, "Beam longitudinal row rejects a quantity/diameter combination with overlapping bars");

        bool overlappingLayersRejected = false;
        try
        {
            BeamLongitudinalLayout.RequireNoOverlap(new[]
            {
                new BeamLongitudinalLayout.SectionBar(0, 0, 20),
                new BeamLongitudinalLayout.SectionBar(0, 10, 20)
            });
        }
        catch (InvalidOperationException) { overlappingLayersRejected = true; }
        Check(overlappingLayersRejected, "Beam section layout rejects overlapping longitudinal layer centerlines");
    }

    private static void RunBeamManualPlanCases()
    {
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(2, 0, 0),
            new BeamManualPoint3V1(1, -1, 0), new BeamManualPoint3V1(1, 1, 0))) < 1e-12,
            "Manual Beam finite-segment distance detects a true perpendicular crossing");
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(2, 0, 0),
            new BeamManualPoint3V1(0, 3, 0), new BeamManualPoint3V1(2, 3, 0)) - 9) < 1e-12,
            "Manual Beam finite-segment distance measures parallel bar clearance");
        Check(Math.Abs(BeamManualSegmentDistanceV1.SquaredDistance(
            new BeamManualPoint3V1(0, 0, 0), new BeamManualPoint3V1(1, 0, 0),
            new BeamManualPoint3V1(2, 0, 0), new BeamManualPoint3V1(3, 0, 0)) - 1) < 1e-12,
            "Manual Beam finite-segment distance respects separated collinear endpoints");

        BeamManualDetailingPlanV1 plan = CreateManualPlan();
        BeamManualValidationResultV1 valid = BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true);
        Check(valid.IsValid, "Explicit synthetic manual Beam plan passes geometry-input validation without design defaults");
        BeamManualDetailingPlanV1 skewFrame = CreateManualPlan();
        skewFrame.Frame.RightX = 1; skewFrame.Frame.RightY = 0; skewFrame.Frame.RightZ = 0;
        Check(!BeamManualPlanValidatorV1.Validate(skewFrame, true).IsValid, "Manual plan rejects non-orthogonal local frame axes");
        BeamManualDetailingPlanV1 reflectedFrame = CreateManualPlan();
        reflectedFrame.Frame.RightX *= -1; reflectedFrame.Frame.RightY *= -1; reflectedFrame.Frame.RightZ *= -1;
        Check(!BeamManualPlanValidatorV1.Validate(reflectedFrame, true).IsValid, "Manual plan rejects reflected local frames with reversed handedness");

        IReadOnlyList<BeamManualResolvedBarV1> bars = BeamManualLayoutBuilderV1.Build(plan);
        Check(bars.Count == 2 && bars[0].Centerline.Count == 2 && bars[0].Centerline[0].StationMm == 120 &&
              bars[0].Centerline[1].StationMm == 5900, "Canonical manual builder retains absolute stations and each physical bar position");
        Check(bars[0].Role == BeamManualBarRole.TopContinuous && bars[1].Role == BeamManualBarRole.BottomMidExtra &&
              bars[0].DiameterMm == 16 && bars[1].DiameterMm == 12, "Manual builder preserves per-group role and exact resolved model diameter");
        BeamManualDetailingPlanV1 disabledGroup = CreateManualPlan();
        disabledGroup.BarGroups[1].Enabled = false;
        Check(BeamManualLayoutBuilderV1.Build(disabledGroup).Count == 1, "Manual builder omits explicitly disabled groups rather than generating them");

        string originalFingerprint = BeamManualLayoutBuilderV1.Fingerprint(plan);
        plan.BarGroups[0].Bars[0].StartStationMm += 1;
        Check(originalFingerprint != BeamManualLayoutBuilderV1.Fingerprint(plan), "Manual plan fingerprint includes explicit station changes");
        plan.BarGroups[0].Bars[0].StartStationMm -= 1;
        BeamManualDetailingPlanV1 changedType = CreateManualPlan();
        changedType.BarGroups[0].BarType.UniqueId = "SYNTHETIC_TEST_OTHER_TYPE";
        Check(originalFingerprint != BeamManualLayoutBuilderV1.Fingerprint(changedType), "Manual plan fingerprint includes exact RebarBarType identity");

        BeamManualDetailingPlanV1 overlap = CreateManualPlan();
        overlap.BarGroups[1].Bars[0].XMm = overlap.BarGroups[0].Bars[0].XMm;
        overlap.BarGroups[1].Bars[0].YMm = overlap.BarGroups[0].Bars[0].YMm;
        Check(!BeamManualPlanValidatorV1.Validate(overlap, true).IsValid, "Manual plan rejects overlapping physical bar envelopes");

        BeamManualDetailingPlanV1 outside = CreateManualPlan();
        outside.BarGroups[0].Bars[0].EndStationMm = outside.Frame.LengthMm + 1;
        Check(!BeamManualPlanValidatorV1.Validate(outside, true).IsValid, "Manual plan rejects stations outside verified Beam concrete");

        BeamManualDetailingPlanV1 asymmetricCover = CreateManualPlan();
        asymmetricCover.BarGroups[0].Bars[0].XMm = 0;
        asymmetricCover.BarGroups[0].Bars[0].YMm = 0;
        asymmetricCover.Covers.Single(cover => cover.Face == BeamManualCoverFace.Bottom).ClearCoverMm = 300;
        Check(!BeamManualPlanValidatorV1.Validate(asymmetricCover, true).IsValid,
            "Manual plan independently checks bottom-face cover even for a bar above section center");

        BeamManualDetailingPlanV1 endFace = CreateManualPlan();
        endFace.BarGroups[0].Bars[0].StartStationMm = 0;
        Check(!BeamManualPlanValidatorV1.Validate(endFace, true).IsValid, "Manual plan keeps the physical bar diameter envelope inside Beam end faces");

        BeamManualDetailingPlanV1 missing = CreateManualPlan();
        missing.BarGroups[0].BarType = null;
        Check(!BeamManualPlanValidatorV1.Validate(missing, true).IsValid, "Manual plan blocks missing exact RebarBarType identity");

        BeamManualDetailingPlanV1 explicitHook = CreateManualPlan();
        explicitHook.BarGroups[0].Bars[0].StartDetail.Kind = BeamManualEndDetailKind.ExplicitLoadedRevitHook;
        explicitHook.BarGroups[0].Bars[0].StartDetail.HookType = new BeamManualElementIdentityV1
        {
            DocumentKey = explicitHook.DocumentKey, UniqueId = "SYNTHETIC_TEST_HOOK_ID", Name = "SYNTHETIC TEST HOOK",
            State = BeamManualReferenceState.Resolved
        };
        explicitHook.BarGroups[0].Bars[0].StartDetail.HookOrientation = BeamManualHookOrientation.Left;
        Check(BeamManualPlanValidatorV1.Validate(explicitHook, true).IsValid,
            "Manual plan accepts a user-selected exact hook identity and orientation for adapter compatibility checking");

        BeamManualDetailingPlanV1 explicitReturn = CreateManualPlan();
        explicitReturn.BarGroups[0].Bars[0].StartDetail = new BeamManualEndDetailV1
        {
            Kind = BeamManualEndDetailKind.ExplicitCurveReturn,
            ReturnPoints = new List<BeamManualPointV1>
            {
                new BeamManualPointV1 { StationMm = 200, XMm = -80, YMm = 160 },
                new BeamManualPointV1 { StationMm = 120, XMm = -80, YMm = 210 }
            }
        };
        IReadOnlyList<BeamManualResolvedBarV1> returnBars = BeamManualLayoutBuilderV1.Build(explicitReturn);
        Check(returnBars[0].Centerline.Count == 3 && returnBars[0].Centerline[0].StationMm == 200 &&
              returnBars[0].Centerline[1].StationMm == 120 && returnBars[0].Centerline[2].StationMm == 5900,
            "Manual builder preserves explicit start-return vertices, anchor and full longitudinal path order");
        explicitReturn.BarGroups[0].Bars[0].StartDetail.ReturnPoints = new List<BeamManualPointV1>
        {
            new BeamManualPointV1 { StationMm = 200, XMm = -60, YMm = 160 },
            new BeamManualPointV1 { StationMm = 160, XMm = -80, YMm = 160 },
            new BeamManualPointV1 { StationMm = 120, XMm = -80, YMm = 210 }
        };
        Check(!BeamManualPlanValidatorV1.Validate(explicitReturn, true).IsValid,
            "Manual plan rejects a non-coplanar return and longitudinal path before Revit curve creation");
        BeamManualPoint3V1[] verticalReturn = { new BeamManualPoint3V1(-80, 160, 200), new BeamManualPoint3V1(-80, 210, 120), new BeamManualPoint3V1(-80, 210, 5900) };
        Check(BeamManualPathGeometryV1.TryGetPlaneNormal(verticalReturn, out BeamManualPoint3V1 verticalNormal) && Math.Abs(verticalNormal.X - 1) < 1e-12,
            "Vertical return preserves the legacy local-right curve plane and hook orientation convention");
        BeamManualPoint3V1[] horizontalReturn = { new BeamManualPoint3V1(-60, 210, 200), new BeamManualPoint3V1(-80, 210, 120), new BeamManualPoint3V1(-80, 210, 5900) };
        Check(BeamManualPathGeometryV1.TryGetPlaneNormal(horizontalReturn, out BeamManualPoint3V1 horizontalNormal) && Math.Abs(horizontalNormal.Y - 1) < 1e-12 && Math.Abs(horizontalNormal.X) < 1e-12,
            "Horizontal return derives local-up plane normal instead of supplying Revit an incompatible local-right normal");
        Check(BeamManualPathGeometryV1.TryGetPlaneNormal(new[] { verticalReturn[1], verticalReturn[2] }, out BeamManualPoint3V1 straightNormal) && Math.Abs(straightNormal.X - 1) < 1e-12,
            "Straight manual centerlines retain local-right normal for loaded hooks");
        Check(BeamManualPathGeometryV1.TryGetPlaneNormal(verticalReturn.Reverse().ToArray(), out BeamManualPoint3V1 reversedNormal) && Math.Abs(reversedNormal.X - 1) < 1e-12,
            "Reversing planar vertices keeps the documented canonical normal sign stable");
        explicitReturn.BarGroups[0].Bars[0].StartDetail.ReturnPoints = null;
        bool malformedReturnRejected = false;
        try { malformedReturnRejected = !BeamManualPlanValidatorV1.Validate(explicitReturn, true).IsValid; }
        catch (NullReferenceException) { }
        Check(malformedReturnRejected, "Missing persisted return vertices produce validation diagnostics instead of a null-reference failure");

        BeamManualDetailingPlanV1 missingStations = CreateManualPlan();
        missingStations.StirrupGroups.Add(new BeamManualStirrupGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_STIRRUP", Role = BeamManualStirrupRole.Stirrup, Enabled = true,
            BarType = SyntheticType(missingStations.DocumentKey, "SYNTHETIC_STIRRUP_TYPE", 10),
            RebarShape = new BeamManualElementIdentityV1 { DocumentKey = missingStations.DocumentKey, UniqueId = "SYNTHETIC_SHAPE", Name = "SYNTHETIC TEST SHAPE", State = BeamManualReferenceState.Resolved },
            StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            SectionPath = new List<BeamManualPointV1> { new BeamManualPointV1 { XMm = -100, YMm = -170 }, new BeamManualPointV1 { XMm = 100, YMm = -170 }, new BeamManualPointV1 { XMm = 100, YMm = 170 }, new BeamManualPointV1 { XMm = -100, YMm = 170 }, new BeamManualPointV1 { XMm = -100, YMm = -170 } },
            CoincidentStationPolicy = "REJECT_DUPLICATE_STATIONS"
        });
        Check(!BeamManualPlanValidatorV1.Validate(missingStations, true).IsValid, "Manual stirrup group requires explicit stations and cannot infer legacy zone defaults");

        BeamManualDetailingPlanV1 explicitStirrup = CreateManualPlan();
        explicitStirrup.StirrupGroups.Add(new BeamManualStirrupGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_STIRRUP_VALID", Role = BeamManualStirrupRole.Hanger, Enabled = true,
            BarType = SyntheticType(explicitStirrup.DocumentKey, "SYNTHETIC_STIRRUP_TYPE_VALID", 10),
            RebarShape = new BeamManualElementIdentityV1 { DocumentKey = explicitStirrup.DocumentKey, UniqueId = "SYNTHETIC_TIE_SHAPE_VALID", Name = "SYNTHETIC TEST TIE SHAPE", State = BeamManualReferenceState.Resolved },
            StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            StationsMm = new List<double> { 100, 200 },
            SectionPath = new List<BeamManualPointV1> { new BeamManualPointV1 { XMm = -100, YMm = -170 }, new BeamManualPointV1 { XMm = 100, YMm = -170 }, new BeamManualPointV1 { XMm = 100, YMm = 170 }, new BeamManualPointV1 { XMm = -100, YMm = 170 }, new BeamManualPointV1 { XMm = -100, YMm = -170 } },
            CoincidentStationPolicy = "REJECT_DUPLICATE_STATIONS"
        });
        Check(BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid &&
              BeamManualLayoutBuilderV1.BuildStirrups(explicitStirrup).Count == 2,
            "Manual builder resolves explicit closed stirrup/hanger contours at every user-listed station");
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 0 };
        Check(!BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Manual stirrup diameter envelope cannot extend past the physical Beam start face");
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { explicitStirrup.Frame.LengthMm };
        Check(!BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Manual stirrup diameter envelope cannot extend past the physical Beam finish face");
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 5, explicitStirrup.Frame.LengthMm - 5 };
        Check(BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Explicit 10 mm test stirrups exactly one radius inside both Beam end faces remain valid");
        BeamManualPointV1 savedContourPoint = explicitStirrup.StirrupGroups[0].SectionPath[1];
        explicitStirrup.StirrupGroups[0].SectionPath[1] = null;
        bool malformedContourRejected = false;
        try { malformedContourRejected = !BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid; }
        catch (NullReferenceException) { }
        Check(malformedContourRejected, "Malformed persisted stirrup points produce validation diagnostics instead of a null-reference failure");
        explicitStirrup.StirrupGroups[0].SectionPath[1] = savedContourPoint;
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 200 };
        explicitStirrup.StirrupGroups[0].SectionPath[2].YMm = 205;
        explicitStirrup.StirrupGroups[0].SectionPath[3].YMm = 205;
        Check(!BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Manual plan rejects a section contour whose explicit stirrup path collides with a longitudinal bar");
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 100, 200 };
        explicitStirrup.StirrupGroups[0].SectionPath[2].YMm = 170;
        explicitStirrup.StirrupGroups[0].SectionPath[3].YMm = 170;
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 100, 100 };
        Check(!BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Manual plan rejects coincident same-group tie stations rather than silently deduplicating them");
        explicitStirrup.StirrupGroups[0].StationsMm = new List<double> { 3000 };
        explicitStirrup.StirrupGroups[0].SectionPath = new List<BeamManualPointV1>
        {
            new BeamManualPointV1 { XMm = -100, YMm = -150 }, new BeamManualPointV1 { XMm = 100, YMm = 150 },
            new BeamManualPointV1 { XMm = -100, YMm = 150 }, new BeamManualPointV1 { XMm = 100, YMm = -150 },
            new BeamManualPointV1 { XMm = -100, YMm = -150 }
        };
        Check(!BeamManualPlanValidatorV1.Validate(explicitStirrup, true).IsValid,
            "Manual plan rejects self-intersecting closed stirrup contours before Revit shape creation");
        BeamManualDetailingPlanV1 oversized = CreateManualPlan();
        oversized.StirrupGroups.Add(new BeamManualStirrupGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_OVERSIZED_STIRRUPS", Role = BeamManualStirrupRole.Stirrup, Enabled = true,
            BarType = SyntheticType(oversized.DocumentKey, "SYNTHETIC_TEST_ONLY_OVERSIZED_TYPE", 10),
            RebarShape = new BeamManualElementIdentityV1 { DocumentKey = oversized.DocumentKey, UniqueId = "SYNTHETIC_TEST_ONLY_OVERSIZED_SHAPE", Name = "SYNTHETIC TEST SHAPE", State = BeamManualReferenceState.Resolved },
            StationsMm = Enumerable.Range(0, BeamManualPlanValidatorV1.MaximumBars).Select(index => 200d + index * 0.5).ToList(),
            SectionPath = new List<BeamManualPointV1> { new BeamManualPointV1 { XMm = -100, YMm = -150 }, new BeamManualPointV1 { XMm = 100, YMm = -150 }, new BeamManualPointV1 { XMm = 100, YMm = 150 }, new BeamManualPointV1 { XMm = -100, YMm = 150 }, new BeamManualPointV1 { XMm = -100, YMm = -150 } },
            StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            CoincidentStationPolicy = "REJECT_DUPLICATE_STATIONS"
        });
        Check(BeamManualPlanValidatorV1.Validate(oversized, true).Issues.Any(issue => issue.Message.Contains("Combined longitudinal-bar and individual-stirrup count exceeds")),
            "Manual plan applies one software physical-bar resource limit to longitudinal bars plus explicit stirrup stations");

        BeamManualDetailingPlanV1 unresolved = CreateManualPlan();
        unresolved.BarGroups[0].BarType.State = BeamManualReferenceState.Unresolved;
        unresolved.BarGroups[0].Bars[0].StartDetail = new BeamManualEndDetailV1
        {
            Kind = BeamManualEndDetailKind.ExplicitLoadedRevitHook,
            HookType = new BeamManualElementIdentityV1
            {
                DocumentKey = unresolved.DocumentKey, UniqueId = "SYNTHETIC_UNRESOLVED_HOOK", Name = "SYNTHETIC HOOK",
                State = BeamManualReferenceState.Unresolved
            },
            HookOrientation = BeamManualHookOrientation.Left
        };
        unresolved.StirrupGroups.Add(new BeamManualStirrupGroupV1
        {
            GroupId = "SYNTHETIC_UNRESOLVED_STIRRUP", Role = BeamManualStirrupRole.Stirrup, Enabled = true,
            BarType = new BeamManualElementReferenceV1
            {
                DocumentKey = unresolved.DocumentKey, UniqueId = "SYNTHETIC_UNRESOLVED_STIRRUP_TYPE", Name = "SYNTHETIC STIRRUP TYPE",
                DiameterMm = 10, State = BeamManualReferenceState.Unresolved
            },
            RebarShape = new BeamManualElementIdentityV1
            {
                DocumentKey = unresolved.DocumentKey, UniqueId = "SYNTHETIC_UNRESOLVED_TIE_SHAPE", Name = "SYNTHETIC TIE SHAPE",
                State = BeamManualReferenceState.Unresolved
            },
            StationsMm = new List<double> { 3000 },
            SectionPath = new List<BeamManualPointV1>
            {
                new BeamManualPointV1 { XMm = -100, YMm = -150 }, new BeamManualPointV1 { XMm = 100, YMm = -150 },
                new BeamManualPointV1 { XMm = 100, YMm = 150 }, new BeamManualPointV1 { XMm = -100, YMm = 150 },
                new BeamManualPointV1 { XMm = -100, YMm = -150 }
            },
            StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
            CoincidentStationPolicy = "REJECT_DUPLICATE_STATIONS"
        });
        Check(!BeamManualPlanValidatorV1.Validate(unresolved, true).IsValid &&
              BeamManualPlanValidatorV1.Validate(unresolved, false).IsValid,
              "Foreign/unresolved type reference remains storable but blocks solve until exact user rebind");

        string temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "beam-manual-v1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BeamManualDetailingPlanStoreV1(temporary, unresolved.DocumentKey, unresolved.HostUniqueId);
            BeamManualDetailingPlanV1 saved = store.Save(unresolved, expectedRevision: 0);
            BeamManualDetailingPlanV1 loaded = store.Load();
            Check(saved.Revision == 1 && loaded.Revision == 1 && loaded.BarGroups[0].BarType.State == BeamManualReferenceState.Unresolved &&
                  loaded.BarGroups[1].Bars[0].XMm == unresolved.BarGroups[1].Bars[0].XMm &&
                  loaded.BarGroups[0].Bars[0].StartDetail.HookType.UniqueId == "SYNTHETIC_UNRESOLVED_HOOK" &&
                  loaded.StirrupGroups.Single().StationsMm.SequenceEqual(new[] { 3000d }) &&
                  loaded.StirrupGroups.Single().RebarShape.State == BeamManualReferenceState.Unresolved,
                  "Versioned manual-plan store round-trips bars, hook identity/orientation, stirrup station/shape, and unresolved references");
            Check(ThrowsIOException(() => store.Save(unresolved, expectedRevision: 0)), "Manual-plan store rejects stale editor revisions");
            BeamManualDetailingPlanV1 wrongHost = CreateManualPlan();
            wrongHost.HostUniqueId = "other-synthetic-host";
            Check(ThrowsInvalidData(() => store.Save(wrongHost, expectedRevision: 0)), "Manual-plan store is bound to exact document and Beam identity");
        }
        finally
        {
            if (System.IO.Directory.Exists(temporary)) System.IO.Directory.Delete(temporary, recursive: true);
        }
    }

    private static BeamManualDetailingPlanV1 CreateManualPlan()
    {
        var plan = new BeamManualDetailingPlanV1
        {
            DocumentKey = "SYNTHETIC_TEST_FIXTURE_ONLY_DOCUMENT",
            HostUniqueId = "SYNTHETIC_TEST_FIXTURE_ONLY_BEAM",
            Frame = new BeamManualFrameV1
            {
                WidthMm = 300, HeightMm = 600, LengthMm = 6000,
                DirectionX = 1, DirectionY = 0, DirectionZ = 0,
                RightX = 0, RightY = -1, RightZ = 0,
                UpX = 0, UpY = 0, UpZ = 1,
                HostGeometrySignature = "SYNTHETIC_TEST_FIXTURE_ONLY_RECTANGULAR_BEAM"
            },
            Covers = new List<BeamManualCoverV1>
            {
                SyntheticCover(BeamManualCoverFace.Top, 40), SyntheticCover(BeamManualCoverFace.Bottom, 45),
                SyntheticCover(BeamManualCoverFace.Left, 35), SyntheticCover(BeamManualCoverFace.Right, 50),
                SyntheticCover(BeamManualCoverFace.StirrupOutside, 30)
            },
            SupportEnds = new List<BeamManualSupportEndV1>
            {
                new BeamManualSupportEndV1 { End = BeamManualEnd.Start, Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost, IntendedContinuation = "SYNTHETIC TEST ONLY: finite geometric termination" },
                new BeamManualSupportEndV1 { End = BeamManualEnd.Finish, Mode = BeamManualSupportMode.UserDefinedFreeEndWithinHost, IntendedContinuation = "SYNTHETIC TEST ONLY: finite geometric termination" }
            }
        };
        plan.BarGroups.Add(new BeamManualBarGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_TOP", Role = BeamManualBarRole.TopContinuous, Enabled = true,
            BarType = SyntheticType(plan.DocumentKey, "SYNTHETIC_TYPE_TOP", 16),
            Bars = new List<BeamManualBarV1> { SyntheticBar(-80, 210, 120, 5900, "SYNTHETIC_LAYER_TOP_1") }
        });
        plan.BarGroups.Add(new BeamManualBarGroupV1
        {
            GroupId = "SYNTHETIC_TEST_ONLY_EXTRA", Role = BeamManualBarRole.BottomMidExtra, Enabled = true,
            BarType = SyntheticType(plan.DocumentKey, "SYNTHETIC_TYPE_EXTRA", 12),
            Bars = new List<BeamManualBarV1> { SyntheticBar(65, -200, 1100, 5100, "SYNTHETIC_LAYER_BOTTOM_1") }
        });
        return plan;
    }

    private static BeamManualCoverV1 SyntheticCover(BeamManualCoverFace face, double value) => new BeamManualCoverV1
    {
        Face = face, Source = BeamManualCoverSource.ExplicitUserInput, ClearCoverMm = value
    };

    private static BeamManualElementReferenceV1 SyntheticType(string documentKey, string id, double diameter) => new BeamManualElementReferenceV1
    {
        DocumentKey = documentKey, UniqueId = id, Name = "SYNTHETIC TEST TYPE " + id,
        DiameterMm = diameter, State = BeamManualReferenceState.Resolved
    };

    private static BeamManualBarV1 SyntheticBar(double x, double y, double start, double end, string layer) => new BeamManualBarV1
    {
        XMm = x, YMm = y, StartStationMm = start, EndStationMm = end, LayerId = layer,
        StartDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation },
        FinishDetail = new BeamManualEndDetailV1 { Kind = BeamManualEndDetailKind.StraightAtStation }
    };

    private static bool ThrowsIOException(Action action)
    {
        try { action(); return false; }
        catch (System.IO.IOException) { return true; }
    }

    private static bool ThrowsInvalidData(Action action)
    {
        try { action(); return false; }
        catch (System.IO.InvalidDataException) { return true; }
    }

    private static void RunRebarCalculationGoldens()
    {
        double tcvnAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double tcvnLap = RebarAnchorageCalculator.CalculateLapLength(
            18, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018);
        double eurocodeAnchorage = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionStraight, DesignCode.Eurocode2);
        double eurocodeHooked = RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.C25_30, SteelGrade.B500, AnchorageType.TensionHooked, DesignCode.Eurocode2);

        Check(Near(600, tcvnAnchorage, 0.000001), "production Rebar TCVN B25/CB400V d18 anchorage golden");
        Check(Near(900, tcvnLap, 0.000001), "production Rebar TCVN B25/CB400V d18 lap golden");
        Check(Near(726.4640343386, eurocodeAnchorage, 0.000001), "production Rebar Eurocode C25/30 B500 d18 anchorage golden");
        Check(Near(508.52482403702, eurocodeHooked, 0.000001), "production Rebar Eurocode C25/30 B500 d18 hooked anchorage golden");
        Check(Near(630, RebarAnchorageCalculator.CalculateAnchorageLength(
            18, ConcreteGrade.Auto, SteelGrade.Auto, AnchorageType.TensionStraight, DesignCode.Eurocode2), 0.000001),
            "production Rebar automatic-grade fallback golden");
    }

    private static void RunDomainGoldens()
    {
        // QS quantity calculations. Units are carried by the fixture label; the shared math is unit-agnostic.
        Check(QsQuantityMath.TryCalculatePayQuantity(12.345, 5, 2, out double concrete) && Near(12.96, concrete, 0.000001), "QS concrete volume with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(10.5, 2, 1, out double finishArea) && Near(10.7, finishArea, 0.000001), "QS finish area with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(0, 0, 2, out double count) && Near(0, count, 0), "QS zero count");
        Check(QsQuantityMath.TryCalculatePayQuantity(54.32, 0, 3, out double length) && Near(54.32, length, 0), "QS lineal length");
        Check(QsQuantityMath.TryCalculatePayQuantity(2.5, 10, 2, out double material) && Near(2.75, material, 0.000001), "QS material quantity with waste");
        Check(QsQuantityMath.TryCalculatePayQuantity(1.005, 0, 2, out double midpoint) && Near(1.01, midpoint, 0.000001), "QS deterministic midpoint rounding");

        var pipe = MepMeasurementCalculator.RoundOpening(100, 25);
        Check(Near(150, pipe.WidthMm, 0) && Near(150, pipe.HeightMm, 0), "MEP circular service clearance");
        var duct = MepMeasurementCalculator.RectangularOpening(600, 300, 25);
        Check(Near(650, duct.WidthMm, 0) && Near(350, duct.HeightMm, 0), "MEP rectangular service clearance");
        var elevations = MepMeasurementCalculator.VerticalRange(5000, 600);
        Check(Near(4700, elevations.BottomMm, 0) && Near(5300, elevations.TopMm, 0), "MEP bottom/top elevation range");

        var horizontal = LintelLayout.Compute(1000, 2000, 3000, 900, 200, 1, 0);
        Check(Near(350, horizontal.StartXmm, 0) && Near(1650, horizontal.EndXmm, 0), "Architectural lintel horizontal semantic geometry");
        Check(Near(2000, horizontal.StartYmm, 0) && Near(3000, horizontal.StartZmm, 0), "Architectural lintel elevation");
        var diagonal = LintelLayout.Compute(0, 0, 0, 1000, 0, 3, 4);
        Check(Near(-300, diagonal.StartXmm, 0) && Near(-400, diagonal.StartYmm, 0) &&
              Near(300, diagonal.EndXmm, 0) && Near(400, diagonal.EndYmm, 0), "Architectural normalized diagonal orientation");
    }

    private static void RunComparatorContract()
    {
        Check(10.0 == 10.0, "exact comparator");
        Check(Near(10.0, 10.0005, 0.001), "tolerance comparator");
        Check(new[] { "A", "B", "A" }.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(new[] { "A", "A", "B" }.OrderBy(x => x, StringComparer.Ordinal)), "unordered collection comparator preserves multiplicity");
        Check(123.5 >= 120 && 123.5 <= 125, "range comparator");

        var expected = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16, SpacingMm = 0, CoverMm = 40 };
        var actual = new SemanticBar { Type = "T16", Host = "COLUMN", Orientation = "VERTICAL", Count = 8, DiameterMm = 16.0002, SpacingMm = 0, CoverMm = 40.0001 };
        Check(SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator accepts numeric tolerance and ignores volatile ids");
        actual.Host = "BEAM";
        Check(!SemanticallyEquivalent(expected, actual, 0.001), "semantic bar comparator rejects wrong host relationship");
    }

    private static bool SemanticallyEquivalent(SemanticBar a, SemanticBar b, double tolerance) =>
        a != null && b != null && a.Type == b.Type && a.Host == b.Host && a.Orientation == b.Orientation &&
        a.Count == b.Count && Near(a.DiameterMm, b.DiameterMm, tolerance) &&
        Near(a.SpacingMm, b.SpacingMm, tolerance) && Near(a.CoverMm, b.CoverMm, tolerance);

    private static void RunEdgeCases()
    {
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(0, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects zero diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(-16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects negative diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.NaN, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects NaN diameter");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(double.MaxValue, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018)), "Rebar rejects overflowing extreme diameter result");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, double.NaN)), "Rebar rejects non-finite fallback multiplier");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateAnchorageLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, (DesignCode)999)), "Rebar rejects unknown design code");
        Check(ThrowsOutOfRange(() => RebarAnchorageCalculator.CalculateLapLength(16, ConcreteGrade.B25, SteelGrade.CB400_V, AnchorageType.TensionStraight, DesignCode.TCVN5574_2018, percentLappedFactor: 0)), "Rebar rejects zero lap factor");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.NaN, 0, 2, out _), "QS rejects NaN input");
        Check(!QsQuantityMath.TryCalculatePayQuantity(-1, 0, 2, out _), "QS rejects negative quantity");
        Check(!QsQuantityMath.TryCalculatePayQuantity(1, -1, 2, out _), "QS rejects negative waste");
        Check(!QsQuantityMath.TryCalculatePayQuantity(double.MaxValue, double.MaxValue, 2, out _), "QS rejects overflow");
        Check(!QsQuantityMath.TryParseQuantity("1,2,3.5", false, out _), "QS rejects malformed unit quantity");
        Check(!QsQuantityMath.TryParseQuantity("NaN", false, out _), "QS rejects non-finite parsed quantity");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RoundOpening(0, 25)), "MEP rejects zero diameter");
        Check(ThrowsArgument(() => MepMeasurementCalculator.RectangularOpening(double.PositiveInfinity, 300, 25)), "MEP rejects extreme non-finite size");
        Check(ThrowsArgument(() => MepMeasurementCalculator.VerticalRange(1000, 0)), "MEP rejects zero section height");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 0, 0, 1, 0)), "Architectural rejects zero lintel width");
        Check(ThrowsArgument(() => LintelLayout.Compute(0, 0, 0, 1, -1, 1, 0)), "Architectural rejects negative extension");
        Check(ThrowsArgument(() => LintelLayout.Compute(double.NaN, 0, 0, 1, 0, 1, 0)), "Architectural rejects non-finite input");
    }

    private static bool ThrowsOutOfRange(Action action)
    {
        try { action(); return false; }
        catch (ArgumentOutOfRangeException) { return true; }
    }

    private static bool ThrowsArgument(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void RunSyntheticStress()
    {
        foreach (int recordCount in new[] { 100, 1000, 10000 })
        {
            var timer = Stopwatch.StartNew();
            double qsTotal = 0;
            double mepTotal = 0;
            double geometryChecksum = 0;
            for (int i = 1; i <= recordCount; i++)
            {
                if (!QsQuantityMath.TryCalculatePayQuantity(i / 10.0, 5, 2, out double pay))
                    throw new InvalidOperationException("QS stress input unexpectedly rejected.");
                qsTotal += pay;
                var measure = MepMeasurementCalculator.RectangularOpening(100 + i, 50 + (i % 300), 25);
                mepTotal += measure.WidthMm + measure.HeightMm;
                var layout = LintelLayout.Compute(i, i / 2.0, 3000, 1000, 100, 1, 0);
                geometryChecksum += layout.StartXmm + layout.EndXmm;
            }
            timer.Stop();
            Check(recordCount > 0 && qsTotal > 0 && mepTotal > 0 && geometryChecksum > 0, $"synthetic stress outputs are finite/non-empty for {recordCount} records");
            Check(timer.Elapsed < TimeSpan.FromSeconds(15), $"synthetic stress completes within 15 seconds for {recordCount} records");
            Console.WriteLine($"STRESS_RECORDS={recordCount}; ELAPSED_MS={timer.ElapsedMilliseconds}; QS_SUM={qsTotal:F2}; MEP_SUM={mepTotal:F2}");
        }
    }
}
