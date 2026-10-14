using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public static class RebarSolvedCenterlineReaderV1
    {
        public static IList<Curve> Read(Rebar bar,int position,bool suppressHooks=false)
        {
            if(bar==null || !bar.IsValidObject || position<0 || position>=bar.NumberOfBarPositions || !bar.DoesBarExistAtPosition(position))
                throw new InvalidOperationException("Exact included Rebar position is required.");
            // Available in every supported2022–2026 API. Includes both distribution
            // and individually moved-bar transforms exactly once, with varying shape.
            var curves=bar.GetTransformedCenterlineCurves(false,suppressHooks,false,MultiplanarOption.IncludeAllMultiplanarCurves,position);
            if(curves==null || curves.Count==0)throw new InvalidOperationException("Revit returned no final-position centerlines; no base-position fallback.");
            return curves;
        }
    }
}
