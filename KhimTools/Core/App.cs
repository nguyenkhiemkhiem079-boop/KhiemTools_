using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using KhimTools.Tools.Updater.Models;
using KhimTools.Tools.Updater.Services;
using KhimTools.Tools.Workspace.Views;

namespace KhimTools.Core
{
    /// <summary>
    /// Application-level entry point cho toàn bộ K-TOOLS.
    /// Chịu trách nhiệm:
    ///   0. Startup Forensic Diagnostics & Version Model tracking (Loaded DLL, SHA256, Build, Commit)
    ///   1. Duplicate Installation & Stale DLL Scanning
    ///   2. Khởi tạo ActionEventHandler (cách ly an toàn)
    ///   3. Đăng ký Dockable Pane Khim Workspace (cách ly an toàn)
    ///   4. Xây dựng Ribbon UI K-TOOLS (cách ly hoàn toàn giữa các panel)
    /// </summary>
    public sealed class App : IExternalApplication
    {
        /// <summary>Dùng khi cần gọi Revit API an toàn từ thread khác (xem Core/ActionEventHandler.cs).</summary>
        public static ActionEventHandler EventHandler { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            if (application == null) return Result.Failed;

            // 0. Ghi nhận thông tin DLL triển khai phục vụ Forensic Diagnostics (Phần 13)
            try
            {
                var verModel = VersionModel.FromAssembly(typeof(App).Assembly);
                RegistrationDiagnostics.RecordWarning("Deployment", 
                    $"Loaded DLL: '{verModel.LoadedAssemblyPath}' | Version: {verModel.ProductVersion} | Build: {verModel.BuildId} | Commit: {verModel.GitCommit} | FileVer: {verModel.FileVersion} | SHA256: {verModel.Sha256Checksum} | Mode: PRODUCTION");

                // Asynchronously scan for duplicate bundles and rogue .addin files (Phần 12)
                Task.Run(() =>
                {
                    try
                    {
                        var scanReport = DuplicateInstallationScanner.Scan();
                        if (scanReport.HasDuplicateConflict)
                        {
                            foreach (var w in scanReport.Warnings)
                            {
                                RegistrationDiagnostics.RecordWarning("DuplicateBundleConflict", w);
                            }
                            RegistrationDiagnostics.PersistLog();
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[K-TOOLS] Duplicate scanner error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[K-TOOLS WARN] Lỗi ghi nhận deployment assembly: {ex.Message}");
            }

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

                // Ghi nhận Startup Success Marker (Phần 17)
                try
                {
                    string markerDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KTools");
                    if (!Directory.Exists(markerDir)) Directory.CreateDirectory(markerDir);
                    string markerPath = Path.Combine(markerDir, "startup_success.marker");
                    File.WriteAllText(markerPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Version: 2.7.1 | Status: SUCCESS");
                }
                catch { }
            }
            catch (Exception ex)
            {
                RegistrationDiagnostics.RecordError("RibbonRoot", "Lỗi ngoài dự kiến trong BuildRibbon", ex);
                TaskDialog.Show("K-TOOLS Startup Warning", 
                    "Một số công cụ K-TOOLS có thể không tải được do lỗi môi trường:\n" + ex.Message);
            }
            finally
            {
                RegistrationDiagnostics.PersistLog();
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
