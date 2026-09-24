using System;
using System.Diagnostics;
using System.Collections.Generic;
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
    public class ActionEventHandler : IExternalEventHandler, IDisposable
    {
        private readonly ExternalEvent _externalEvent;
        private readonly object _queueLock = new object();
        private readonly LinkedList<Action<UIApplication>> _pendingActions =
            new LinkedList<Action<UIApplication>>();
        private bool _disposed;

        public ActionEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        /// <summary>Đăng ký 1 action sẽ chạy trên Revit main thread ở lượt idle gần nhất.</summary>
        public void Raise(Action<UIApplication> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (_queueLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ActionEventHandler));
                LinkedListNode<Action<UIApplication>> node = _pendingActions.AddLast(action);
                try
                {
                    ExternalEventRequest request = _externalEvent.Raise();
                    if (request == ExternalEventRequest.Denied || request == ExternalEventRequest.TimedOut)
                    {
                        _pendingActions.Remove(node);
                        Debug.WriteLine("[K-TOOLS] Revit rejected an ExternalEvent request; its action was discarded: " + request);
                    }
                }
                catch
                {
                    _pendingActions.Remove(node);
                    Debug.WriteLine("[K-TOOLS] Revit ExternalEvent could not be raised; its action was discarded.");
                    throw;
                }
            }
        }

        public void Execute(UIApplication app)
        {
            Action<UIApplication>[] actions;
            lock (_queueLock)
            {
                actions = new Action<UIApplication>[_pendingActions.Count];
                _pendingActions.CopyTo(actions, 0);
                _pendingActions.Clear();
            }
            foreach (Action<UIApplication> action in actions)
            {
                try
                {
                    action(app);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[K-TOOLS] ExternalEvent action failed: " + ex);
                }
            }
        }

        public void Dispose()
        {
            lock (_queueLock)
            {
                if (_disposed) return;
                _disposed = true;
                _pendingActions.Clear();
                _externalEvent.Dispose();
            }
        }

        public string GetName() => "KhimTools ActionEventHandler";
    }
}
