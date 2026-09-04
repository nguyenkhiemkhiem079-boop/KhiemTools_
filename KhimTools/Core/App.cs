using System;
using System.Diagnostics;
using Autodesk.Revit.UI;
using KhimTools.Tools.Workspace.Views;

namespace KhimTools.Core
{
    /// <summary>
    /// Application-level entry point cho toàn bộ K-TOOLS.
    /// Chịu trách nhiệm:
    ///   1. Khởi tạo ActionEventHandler (cách ly an toàn)
    ///   2. Đăng ký Dockable Pane Khim Workspace (cách ly an toàn)
    ///   3. Xây dựng Ribbon UI K-TOOLS (cách ly hoàn toàn giữa các panel)
    /// </summary>
    public sealed class App : IExternalApplication
    {
        /// <summary>Dùng khi cần gọi Revit API an toàn từ thread khác (xem Core/ActionEventHandler.cs).</summary>
        public static ActionEventHandler EventHandler { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            if (application == null) return Result.Failed;

            // 1. Khởi tạo ActionEventHandler an toàn
            try
            {
                EventHandler = new ActionEventHandler();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[K-TOOLS WARN] Không thể khởi tạo ActionEventHandler: {ex.Message}");
            }

            // 2. Đăng ký Dockable Pane cho Khim Workspace an toàn
            try
            {
                var workspacePane = new KhimWorkspacePane();
                application.RegisterDockablePane(KhimWorkspacePane.PaneId, "Khim Workspace", workspacePane);
            }
            catch (Exception ex)
            {
                // Dockable Pane có thể đã được đăng ký hoặc gặp hạn chế môi trường Revit, ghi log và tiếp tục
                Trace.WriteLine($"[K-TOOLS INFO] RegisterDockablePane (Khim Workspace): {ex.Message}");
            }

            // 3. Xây dựng Ribbon UI với cơ chế Failure Isolation
            try
            {
                RibbonBuilder.BuildRibbon(application);
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError("RibbonRoot", "Lỗi ngoài dự kiến trong BuildRibbon", ex);
                RegistrationDiagnostics.PersistLog();
                TaskDialog.Show("K-TOOLS Startup Warning", 
                    "Một số công cụ K-TOOLS có thể không tải được do lỗi môi trường:\n" + ex.Message);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
