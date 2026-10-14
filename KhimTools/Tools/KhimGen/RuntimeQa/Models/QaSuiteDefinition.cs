using System;
using System.Collections.Generic;

namespace KhimTools.RuntimeQa.Models
{
    public sealed class QaSuiteDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> FixtureIds { get; set; } = new List<string>();
    }
}
