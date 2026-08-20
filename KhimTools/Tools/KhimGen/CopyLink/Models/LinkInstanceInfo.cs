using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.CopyLink.Models
{
    public class LinkInstanceInfo
    {
        public RevitLinkInstance Instance { get; set; }
        public ElementId InstanceId => Instance?.Id;
        public Document LinkDocument { get; set; }
        public string DisplayName { get; set; }
        public Transform TotalTransform => Instance != null ? Instance.GetTotalTransform() : Transform.Identity;

        public override string ToString() => DisplayName ?? "Revit Link";
    }

    public class LinkCategoryItem
    {
        public ElementId CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int ElementCount => ElementIds?.Count ?? 0;
        public List<ElementId> ElementIds { get; set; } = new List<ElementId>();

        public override string ToString() => $"{CategoryName} ({ElementCount})";
    }
}
