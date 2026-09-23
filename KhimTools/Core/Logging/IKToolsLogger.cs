using KhimTools.Core.Workflow;

namespace KhimTools.Core.Logging
{
    public interface IKToolsLogger
    {
        void Log(WorkflowSeverity severity, string operation, string message, string code = null);
        void Exception(string operation, System.Exception exception, string code = null);
    }
}
