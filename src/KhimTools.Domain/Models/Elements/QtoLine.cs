using System;

namespace KhimTools.Domain.Models.Elements
{
    public sealed class QtoLine
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double PayQuantity { get; set; }
        public string WorkPackage { get; set; } = string.Empty;
        public string ElementUniqueId { get; set; } = string.Empty;
    }
}
