using Autodesk.Revit.UI;

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
            EventHandler = new ActionEventHandler();
            RibbonBuilder.BuildRibbon(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
