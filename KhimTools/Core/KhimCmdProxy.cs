using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Dynamic Command Dispatcher / Hot-Reload Proxy cho KhimTools.
    /// Tự động đọc và nạp byte DLL mới nhất từ thư mục build workspace (bin/Debug/net48 hoặc net8.0-windows)
    /// mỗi khi bấm nút trên Ribbon Revit mà KHÔNG CẦN tắt hay khởi động lại Revit!
    /// </summary>
    public abstract class KhimCmdProxy : IExternalCommand
    {
        protected abstract string TargetCommandClassName { get; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                string buildDllPath = FindWorkspaceBuildDll();

                if (!string.IsNullOrEmpty(buildDllPath) && File.Exists(buildDllPath))
                {
                    byte[] rawBytes = File.ReadAllBytes(buildDllPath);
                    Assembly loadedAsm = Assembly.Load(rawBytes);
                    Type implType = loadedAsm.GetType(TargetCommandClassName);

                    if (implType != null)
                    {
                        var commandImpl = Activator.CreateInstance(implType) as IExternalCommand;
                        if (commandImpl != null)
                        {
                            return commandImpl.Execute(commandData, ref message, elements);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KhimTools HotReload Error] {ex.Message}");
            }

            // Fallback: Nếu không tìm thấy file DLL build hoặc lỗi nạp byte, chạy trực tiếp từ Assembly đang nạp
            try
            {
                Type fallbackType = Assembly.GetExecutingAssembly().GetType(TargetCommandClassName);
                if (fallbackType != null)
                {
                    var fallbackImpl = Activator.CreateInstance(fallbackType) as IExternalCommand;
                    if (fallbackImpl != null)
                    {
                        return fallbackImpl.Execute(commandData, ref message, elements);
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            return Result.Failed;
        }

        private static string FindWorkspaceBuildDll()
        {
            string baseDir = @"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\bin\Debug";
            string net48Path = Path.Combine(baseDir, "net48", "KhimTools.dll");
            string net8Path = Path.Combine(baseDir, "net8.0-windows", "KhimTools.dll");

            if (File.Exists(net8Path)) return net8Path;
            if (File.Exists(net48Path)) return net48Path;

            return null;
        }
    }
}
