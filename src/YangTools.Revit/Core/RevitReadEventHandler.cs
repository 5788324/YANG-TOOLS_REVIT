using System;
using System.Collections.Concurrent;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Read-only ExternalEvent queue for modeless WPF windows.
    /// No Transaction or TransactionGroup is opened.
    /// </summary>
    public class RevitReadEventHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<(Action<UIApplication> action, string name)> _queue
            = new ConcurrentQueue<(Action<UIApplication>, string)>();

        private readonly Document _expectedDocument;

        public RevitReadEventHandler(Document expectedDocument = null)
        {
            _expectedDocument = expectedDocument;
        }

        public void SetAction(Action<UIApplication> action, string operationName = "读取 Revit 数据")
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _queue.Enqueue((action, operationName));
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var item))
            {
                var activeDocument = app.ActiveUIDocument?.Document;

                if (_expectedDocument != null)
                {
                    if (!_expectedDocument.IsValidObject)
                    {
                        TaskDialog.Show("文档已关闭", $"“{item.name}”未执行。此窗口绑定的文档已经关闭。");
                        continue;
                    }

                    if (activeDocument == null || !activeDocument.Equals(_expectedDocument))
                    {
                        TaskDialog.Show(
                            "当前文档已切换",
                            $"“{item.name}”未执行。此窗口绑定到“{_expectedDocument.Title}”。\n请切回该文档，或关闭窗口后重新打开功能。");
                        continue;
                    }
                }

                try
                {
                    item.action.Invoke(app);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User cancellation is not a failure.
                }
                catch (OperationCanceledException)
                {
                    // User cancellation is not a failure.
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError(item.name, ex);
                    TaskDialog.Show("错误", $"{item.name}失败。\n错误信息：{ex.Message}");
                }
            }
        }

        public string GetName() => "YangTools RevitReadEventHandler";
    }
}
