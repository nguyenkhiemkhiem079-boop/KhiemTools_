using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Native exact curved-centerline sweep guard; tessellated chords cannot certify curved clearance.</summary>
    internal static class RebarSolvedEnvelopeCollisionV1
    {
        private static Curve[] Curves(Rebar bar)
        {
            var curves=new List<Curve>();
            for(int i=0;i<bar.NumberOfBarPositions;i++)if(bar.DoesBarExistAtPosition(i))curves.AddRange(RebarSolvedCenterlineReaderV1.Read(bar,i));
            if(curves.Count==0 || curves.Any(c=>!(c is Line || c is Arc)))throw new InvalidOperationException("Unsupported/missing real curves prevent exact Rebar collision proof.");
            return curves.ToArray();
        }
        internal static void Verify(Rebar candidate,IEnumerable<Rebar> neighbors)
        {
            var current=Curves(candidate);var requested=candidate.Document.GetElement(candidate.GetTypeId()) as RebarBarType ?? throw new InvalidOperationException("Exact candidate diameter missing.");
            Solid[] candidateSweeps=null;
            foreach(var other in neighbors) {
                var old=Curves(other);
                if(current.All(c=>c is Line)&&old.All(c=>c is Line))continue; // Existing analytic segment-distance test already covers exact straight paths.
                var type=other.Document.GetElement(other.GetTypeId()) as RebarBarType ?? throw new InvalidOperationException("Exact neighbor diameter missing.");
                if(candidateSweeps==null)candidateSweeps=current.Select(c=>BeamManualSupportGeometryV1.Sweep(c,requested.BarModelDiameter/2)).ToArray();
                var oldSweeps=old.Select(c=>BeamManualSupportGeometryV1.Sweep(c,type.BarModelDiameter/2)).ToArray();
                foreach(var a in candidateSweeps)foreach(var b in oldSweeps) {
                    var overlap=BeamManualSupportGeometryV1.Boolean(a,b,BooleanOperationsType.Intersect);
                    if(overlap.Volume>Math.Max(1e-12,Math.Min(a.Volume,b.Volume)*1e-8))
                        throw new InvalidOperationException("Actual curved Rebar physical envelopes intersect; whole Manual creation must roll back. Chord clearance is not accepted.");
                }
            }
        }
    }
}
