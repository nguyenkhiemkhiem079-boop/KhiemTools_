using System;
using Autodesk.Revit.UI;
using KhimTools.Core.Logging;
using KhimTools.Core.Workflow;
using KhimTools.Tools.Workspace.Views;

namespace KhimTools.Core
{
    /// <summary>
    /// Application-level entry point cho toàn bộ K-TOOLS (Slab Join/Unjoin, Rebar).
    /// Chỉ chịu trách nhiệm dựng ribbon + khởi tạo ActionEventHandler dùng chung lúc khởi
    /// động; logic nghiệp vụ nằm ở từng module con (KhimTools.SlabJoin.*, KhimTools.RebarTool.*).
    /// </summary>
    public sealed class App : IExternalApplication
    {
        /// <summary>Dùng khi cần gọi Revit API an toàn từ thread khác (xem Core/ActionEventHandler.cs).</summary>
        public static ActionEventHandler EventHandler { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            KToolsLog.Current.Log(WorkflowSeverity.Info, "Application.OnStartup", "K-TOOLS startup requested.", "APP_START");
            try
            {
                EventHandler = new ActionEventHandler();
                TryRegisterWorkspacePane(application);
                RibbonBuilder.BuildRibbon(application);
                KToolsLog.Current.Log(
                    WorkflowSeverity.Info,
                    "Application.OnStartup",
                    "K-TOOLS startup completed; Revit=" + application.ControlledApplication.VersionNumber + ".",
                    "APP_READY");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                ActionEventHandler eventHandler = EventHandler;
                EventHandler = null;
                if (eventHandler != null) eventHandler.Dispose();
                KToolsLog.Current.Exception("Application.OnStartup", ex, "STARTUP_FAILED");
                TaskDialog.Show("K-TOOLS Startup Error", "K-TOOLS could not start. " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            ActionEventHandler eventHandler = EventHandler;
            EventHandler = null;
            if (eventHandler != null) eventHandler.Dispose();
            KToolsLog.Current.Log(WorkflowSeverity.Info, "Application.OnShutdown", "K-TOOLS shutdown completed.", "APP_STOP");
            return Result.Succeeded;
        }

        private static void TryRegisterWorkspacePane(UIControlledApplication application)
        {
            try
            {
                application.RegisterDockablePane(
                    KhimWorkspacePane.PaneId,
                    "Khim Workspace",
                    new KhimWorkspacePane());
            }
            catch (Exception ex)
            {
                KToolsLog.Current.Log(WorkflowSeverity.Warning, "Application.RegisterWorkspacePane", ex.Message, "PANE_REGISTRATION_SKIPPED");
            }
        }
    }
}
