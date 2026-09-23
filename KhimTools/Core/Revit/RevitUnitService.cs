namespace KhimTools.Core.Revit
{
    /// <summary>
    /// Revit's internal length unit is feet. Keeping conversion and tolerance in one
    /// adapter service prevents literal 304.8 values from leaking through workflows.
    /// </summary>
    public static class RevitUnitService
    {
        public const double MillimetresPerFoot = 304.8;
        public const double DefaultToleranceMillimetres = 0.1;

        public static double FeetToMillimetres(double feet)
        {
            return feet * MillimetresPerFoot;
        }

        public static double MillimetresToFeet(double millimetres)
        {
            return millimetres / MillimetresPerFoot;
        }

        public static bool NearlyEqualFeet(double left, double right, double toleranceMillimetres = DefaultToleranceMillimetres)
        {
            return System.Math.Abs(left - right) <= MillimetresToFeet(toleranceMillimetres);
        }
    }
}
