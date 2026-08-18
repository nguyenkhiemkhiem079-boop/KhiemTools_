using System;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Cầu nối bắt buộc để gọi Revit API an toàn từ thread khác (WPF window modeless, async
    /// task...). Revit API chỉ cho phép gọi từ đúng main thread của Revit; nếu sau này bạn
    /// build tool dùng WPF window không-modal (Show() thay vì ShowDialog()), MỌI Transaction/
    /// Create/Delete/Set phải bọc qua đây thay vì gọi trực tiếp.
    ///
    /// Các form WinForms hiện tại của SlabJoin/RebarTool KHÔNG cần đổi gì — vì đều gọi
    /// ShowDialog() từ trong IExternalCommand.Execute(), tức đã chạy trên đúng Revit main
    /// thread ngay từ đầu (modal dialog chặn thread cho tới khi đóng), nên không cần
    /// ActionEventHandler cho các form đó.
    /// </summary>
    public class ActionEventHandler : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private Action<UIApplication> _pendingAction;

        public ActionEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        /// <summary>Đăng ký 1 action sẽ chạy trên Revit main thread ở lượt idle gần nhất.</summary>
        public void Raise(Action<UIApplication> action)
        {
            _pendingAction = action;
            _externalEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _pendingAction?.Invoke(app);
            }
            finally
            {
                _pendingAction = null;
            }
        }

        public string GetName() => "KhimTools ActionEventHandler";
    }
}
