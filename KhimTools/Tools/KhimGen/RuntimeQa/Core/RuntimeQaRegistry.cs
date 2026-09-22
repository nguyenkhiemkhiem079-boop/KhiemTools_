using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.RuntimeQa.Fixtures;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    /// <summary>Extensible fixture registry. Production commands never depend on this QA-only namespace.</summary>
    public sealed class RuntimeQaRegistry
    {
        private readonly List<IRuntimeQaFixture> _fixtures = new List<IRuntimeQaFixture>();
        private static readonly RuntimeQaRegistry _default = CreateDefaultInternal();

        public static RuntimeQaRegistry Default { get { return _default; } }
        public IEnumerable<IRuntimeQaFixture> Fixtures { get { return _fixtures; } }

        public void RegisterFixture(IRuntimeQaFixture fixture)
        {
            if (fixture == null || _fixtures.Any(f => string.Equals(f.Id, fixture.Id, StringComparison.OrdinalIgnoreCase))) return;
            _fixtures.Add(fixture);
        }

        public static void Register(IRuntimeQaFixture fixture)
        {
            Default.RegisterFixture(fixture);
        }

        public IRuntimeQaFixture Find(string fixtureId)
        {
            return _fixtures.FirstOrDefault(f => string.Equals(f.Id, fixtureId, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<IRuntimeQaFixture> ForSuite(string suiteId)
        {
            if (string.Equals(suiteId, "FULL", StringComparison.OrdinalIgnoreCase)) return _fixtures;
            return _fixtures.Where(f => string.Equals(f.Suite, suiteId, StringComparison.OrdinalIgnoreCase));
        }

        public IList<QaSuiteDefinition> GetSuites()
        {
            var suites = new List<QaSuiteDefinition>
            {
                new QaSuiteDefinition { Id = "DOCUMENTATION", Name = "Documentation", Description = "SheetGen, viewport, detail number, text, tag, sheet export, Sheet Copy, Split Schedule, Title Block Sync, Filter Manager, Parameter Manager and Modify Objects fixtures." },
                new QaSuiteDefinition { Id = "REBAR", Name = "Rebar", Description = "Production Rebar generation and rollback fixtures." },
                new QaSuiteDefinition { Id = "CORE", Name = "Core", Description = "Runtime and model safety smoke fixtures." },
                new QaSuiteDefinition { Id = "FULL", Name = "Full", Description = "All registered runtime fixtures." }
            };
            foreach (QaSuiteDefinition suite in suites)
                suite.FixtureIds.AddRange(ForSuite(suite.Id).Select(f => f.Id));
            return suites;
        }

        public static RuntimeQaRegistry CreateDefault()
        {
            var copy = new RuntimeQaRegistry();
            foreach (IRuntimeQaFixture fixture in Default.Fixtures) copy.RegisterFixture(fixture);
            return copy;
        }

        private static RuntimeQaRegistry CreateDefaultInternal()
        {
            var registry = new RuntimeQaRegistry();
            registry.RegisterFixture(new SheetGenRuntimeFixture());
            registry.RegisterFixture(new ViewportAlignRuntimeFixture());
            registry.RegisterFixture(new DetailNumberRuntimeFixture());
            registry.RegisterFixture(new TextAlignRuntimeFixture());
            registry.RegisterFixture(new ElementTagsRuntimeFixture());
            registry.RegisterFixture(new SheetExportRuntimeFixture());
            registry.RegisterFixture(new SheetCopyRuntimeFixture());
            registry.RegisterFixture(new ScheduleSplitRuntimeFixture());
            registry.RegisterFixture(new TitleBlockSyncRuntimeFixture());
            registry.RegisterFixture(new FilterManagerRuntimeFixture());
            registry.RegisterFixture(new ParameterManagerRuntimeFixture());
            registry.RegisterFixture(new ModifyObjectsRuntimeFixture());
            registry.RegisterFixture(new RebarCoreRuntimeFixture());
            registry.RegisterFixture(new RectangularColumnRuntimeFixture());
            registry.RegisterFixture(new RectangularColumnFullCageRuntimeFixture());
            registry.RegisterFixture(new CircularColumnRuntimeFixture());
            registry.RegisterFixture(new BeamRebarRuntimeFixture());
            registry.RegisterFixture(new SlabRebarRuntimeFixture());
            registry.RegisterFixture(new FoundationRebarRuntimeFixture());
            return registry;
        }
    }
}
