using System;
using Autodesk.Revit.UI;
using KhimTools.Tools.Workspace.Views;

namespace KhimTools.Core
{
    /// <summary>
    /// Application-level entry point cho toàn bộ Khim Tools.
    /// Chịu trách nhiệm dựng ribbon, đăng ký Dockable Workspace Pane, và khởi tạo ActionEventHandler.
    /// </summary>
    public sealed class App : IExternalApplication
    {
        /// <summary>Dùng khi cần gọi Revit API an toàn từ thread khác (xem Core/ActionEventHandler.cs).</summary>
        public static ActionEventHandler EventHandler { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            EventHandler = new ActionEventHandler();
            
            // 1. Dựng Ribbon theo chuẩn Workspace chuyên nghiệp
            RibbonBuilder.BuildRibbon(application);

            // 2. Đăng ký Dockable Pane cho KhimTools Workspace
            RegisterDockablePane(application);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private void RegisterDockablePane(UIControlledApplication application)
        {
            try
            {
                var paneView = new KhimWorkspacePane();
                application.RegisterDockablePane(KhimWorkspacePane.PaneId, "KhimTools Workspace", paneView);
            }
            catch
            {
                // Tránh throw exception nếu phiên bản hoặc ngữ cảnh chưa hỗ trợ
            }
        }
    }
}
