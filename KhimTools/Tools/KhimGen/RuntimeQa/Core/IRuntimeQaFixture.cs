using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    public interface IRuntimeQaFixture
    {
        string Id { get; }
        string Name { get; }
        string Suite { get; }
        string Description { get; }
        bool IsCritical { get; }
        bool CanRun(RuntimeQaContext context, out string reason);
        QaFixtureResult Run(RuntimeQaContext context);
    }
}
