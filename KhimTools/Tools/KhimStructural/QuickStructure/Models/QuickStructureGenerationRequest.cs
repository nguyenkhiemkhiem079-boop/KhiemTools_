using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.Structural.QuickStructure.Models
{
    /// <summary>Validated choices and inputs for one atomic Quick Structure generation request.</summary>
    public sealed class QuickStructureGenerationRequest
    {
        public IList<Grid> Grids { get; set; }
        public IList<XYZ> Intersections { get; set; }
        public bool CreateColumns { get; set; }
        public FamilySymbol ColumnSymbol { get; set; }
        public Level ColumnBaseLevel { get; set; }
        public Level ColumnTopLevel { get; set; }
        public double ColumnBaseOffsetMm { get; set; }
        public double ColumnTopOffsetMm { get; set; }
        public bool CreateBeams { get; set; }
        public FamilySymbol BeamSymbol { get; set; }
        public Level BeamLevel { get; set; }
        public double BeamZOffsetMm { get; set; }
        public bool CreateFootings { get; set; }
        public FamilySymbol FootingSymbol { get; set; }
        public Level FootingLevel { get; set; }
        public double FootingOffsetMm { get; set; }
    }

    /// <summary>Created element identities for one successfully committed Quick Structure batch.</summary>
    public sealed class QuickStructureGenerationResult
    {
        public IList<FamilyInstance> Columns { get; private set; }
        public IList<FamilyInstance> Beams { get; private set; }
        public IList<FamilyInstance> Footings { get; private set; }

        internal QuickStructureGenerationResult(IList<FamilyInstance> columns,
            IList<FamilyInstance> beams, IList<FamilyInstance> footings)
        {
            Columns = new List<FamilyInstance>(columns ?? new List<FamilyInstance>()).AsReadOnly();
            Beams = new List<FamilyInstance>(beams ?? new List<FamilyInstance>()).AsReadOnly();
            Footings = new List<FamilyInstance>(footings ?? new List<FamilyInstance>()).AsReadOnly();
        }
    }
}
