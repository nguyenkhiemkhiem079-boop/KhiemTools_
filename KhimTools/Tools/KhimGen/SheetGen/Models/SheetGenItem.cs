using System;
using Autodesk.Revit.DB;

namespace KhimTools.SheetGen.Models
{
    public class SheetGenItem
    {
        public bool IsSelected { get; set; } = true;
        public string SheetNumber { get; set; } = "";
        public string SheetName { get; set; } = "";
        public ElementId TitleBlockId { get; set; } = ElementId.InvalidElementId;
        public string TitleBlockName { get; set; } = "";
        public ElementId AssignedViewId { get; set; } = ElementId.InvalidElementId;
        public string AssignedViewName { get; set; } = "";
        public string Discipline { get; set; } = "";
        public string DrawnBy { get; set; } = "";
        public string CheckedBy { get; set; } = "";
    }

    public enum DuplicateSheetMode
    {
        EmptySheet,
        DuplicateWithViews,
        DuplicateWithViewsAndDetails,
        DuplicateAsDependent
    }
}