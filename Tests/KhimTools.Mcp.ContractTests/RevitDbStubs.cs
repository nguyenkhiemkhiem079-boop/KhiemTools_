namespace Autodesk.Revit.DB
{
    // The exercised canonical name-planning services do not touch the Revit API. These compile-only
    // type shapes keep the isolated contract harness independent from an installed Revit runtime.
    public sealed class ElementId { }
    public sealed class ViewSheet { public ElementId Id { get; set; } }
}
