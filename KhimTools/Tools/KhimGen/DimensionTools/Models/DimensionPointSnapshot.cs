namespace KhimTools.DimensionTools.Models
{
    /// <summary>
    /// Detached point/vector value used by a dimension plan. It deliberately has no
    /// Revit API dependency so a preview plan cannot retain an API geometry object.
    /// </summary>
    public sealed class DimensionPointSnapshot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public DimensionPointSnapshot()
        {
        }

        public DimensionPointSnapshot(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
