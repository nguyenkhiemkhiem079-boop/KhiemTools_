using System;
using Autodesk.Revit.UI;
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
            try
            {
                EventHandler = new ActionEventHandler();
                TryRegisterWorkspacePane(application);
                RibbonBuilder.BuildRibbon(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("K-TOOLS Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
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
                System.Diagnostics.Debug.WriteLine("[K-TOOLS] Workspace pane registration skipped: " + ex.Message);
            }
        }
    }
}
