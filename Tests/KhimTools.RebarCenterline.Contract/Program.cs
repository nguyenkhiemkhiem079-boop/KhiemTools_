using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;

// Explicit synthetic API doubles. No Revit assembly/model or native geometry runs.
namespace Autodesk.Revit.DB {public sealed class Curve {public double Station;public Curve(double station){Station=station;}}}
namespace Autodesk.Revit.DB.Structure
{
    public enum MultiplanarOption{IncludeAllMultiplanarCurves}
    public sealed class Rebar
    {
        public bool IsValidObject=true;
        public int NumberOfBarPositions=3;
        public bool Empty,Excluded;
        public bool LastSuppressHooks;
        public int RawCalls,FinalCalls;
        public bool DoesBarExistAtPosition(int i)=>!(Excluded&&i==1);
        public IList<Curve> GetCenterlineCurves(bool a,bool b,bool c,MultiplanarOption m,int i){RawCalls++;return new[]{new Curve(0)};}
        public IList<Curve> GetTransformedCenterlineCurves(bool a,bool b,bool c,MultiplanarOption m,int i){FinalCalls++;LastSuppressHooks=b;return Empty?Array.Empty<Curve>():new[]{new Curve(i*10+(i==2?3:0))};}
    }
}
static class Program
{
    static int checks;
    static void Check(bool p,string message){if(!p)throw new Exception(message);checks++;}
    static bool Reject(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
    static void Main(){
        var bar=new Rebar();Check(RebarSolvedCenterlineReaderV1.Read(bar,0)[0].Station==0,"First position");
        Check(RebarSolvedCenterlineReaderV1.Read(bar,1)[0].Station==10,"Distribution position, not repeated base");
        Check(RebarSolvedCenterlineReaderV1.Read(bar,2)[0].Station==23,"Moved position retained, not double transformed");
        Check(bar.RawCalls==0&&bar.FinalCalls==3,"Actual production adapter calls final API, never raw fallback");
        RebarSolvedCenterlineReaderV1.Read(bar,0,true);Check(bar.LastSuppressHooks,"Reader forwards drawing hook suppression");
        bar.Excluded=true;Check(Reject(()=>RebarSolvedCenterlineReaderV1.Read(bar,1)),"Excluded position cannot silently render");
        bar.Empty=true;Check(Reject(()=>RebarSolvedCenterlineReaderV1.Read(bar,0)),"Missing final curves rejected");
        Check(Reject(()=>RebarSolvedCenterlineReaderV1.Read(null,0))&&Reject(()=>RebarSolvedCenterlineReaderV1.Read(bar,3)),"Null/out of range rejected");
        Console.WriteLine($"REBAR_WORLD_POSITION_CONTRACT=PASS ({checks} assertions); synthetic API only; LIVE_REVIT=NOT_EXECUTED");
    }
}
