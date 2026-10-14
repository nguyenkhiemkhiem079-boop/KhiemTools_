using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    public sealed class SolvedLocalPathV1
    {
        public string Role { get; }
        public IReadOnlyList<SlabSectionPoint> Points { get; }
        public SolvedLocalPathV1(string role,IEnumerable<SlabSectionPoint> points)
        {Role=role??string.Empty;Points=Array.AsReadOnly((points??Enumerable.Empty<SlabSectionPoint>()).ToArray());}
    }
    /// <summary>Revit-free actual solver polylines. x/z are section coordinates; y is the cut normal, all in feet.</summary>
    public sealed class DetachedSolvedViewV1
    {
        public IReadOnlyList<SlabSectionPrimitive> Section { get; }
        public IReadOnlyList<SlabSectionPrimitive> Elevation { get; }
        public double CutFeet { get; }
        public double SectionX0,SectionZ0,SectionWidth,SectionHeight;
        public double ElevationX0,ElevationY0,ElevationWidth,ElevationHeight;
        public bool CircularSection;
        public string HostLabel;
        public DetachedSolvedViewV1(IEnumerable<SolvedLocalPathV1> paths,double cutFeet,bool elevationXY=false)
        {
            if(!Finite(cutFeet))throw new ArgumentException("Finite section location required.");
            CutFeet=cutFeet;
            var section=new List<SlabSectionPrimitive>();var elevation=new List<SlabSectionPrimitive>();
            foreach(var path in paths??Enumerable.Empty<SolvedLocalPathV1>())
            {
                if(path==null || path.Points.Count<2 || path.Points.Any(p=>!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)))continue;
                section.AddRange(SlabSectionGeometry.IntersectPath(path.Points,SlabSectionAxis.SectionX,cutFeet,path.Role));
                for(int i=0;i+1<path.Points.Count;i++) {
                    var a=path.Points[i];var b=path.Points[i+1];
                    elevation.Add(new SlabSectionPrimitive(SlabSectionPrimitiveKind.CoplanarSegment,path.Role,
                        elevationXY?a.X:a.Y,elevationXY?a.Y:a.Z,elevationXY?b.X:b.Y,elevationXY?b.Y:b.Z));
                }
            }
            Section=section.AsReadOnly();Elevation=elevation.AsReadOnly();
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
