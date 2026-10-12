using System;
using System.Collections.Concurrent;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Revit ExternalEvent write queue for modeless WPF windows.
    /// The optional document binding prevents stale ElementIds from being applied
    /// to another active document after the user switches projects.
    /// </summary>
    public class RevitEventHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<(Action<UIApplication> action, string name, bool showSuccess, string operationId)> _queue
            = new ConcurrentQueue<(Action<UIApplication>, string, bool, string)>();

        private readonly Document _expectedDocument;
        private readonly bool _wrapInTransactionGroup;
        private readonly bool _showAutomaticSuccess;

        public RevitEventHandler(
            Document expectedDocument = null,
            bool wrapInTransactionGroup = true,
            bool showAutomaticSuccess = true)
        {
            _expectedDocument = expectedDocument;
            _wrapInTransactionGroup = wrapInTransactionGroup;
            _showAutomaticSuccess = showAutomaticSuccess;
        }

        public void SetAction(
            Action<UIApplication> action,
            string operationName = "YangTools 操作",
            bool showSuccess = true,
            string operationId = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            string id = string.IsNullOrWhiteSpace(operationId) ? Guid.NewGuid().ToString("N") : operationId;
            _queue.Enqueue((action, operationName, showSuccess, id));
        }

        public void Execute(UIApplication app)
        {
            while (_queue.TryDequeue(out var item))
            {
                if (!TryResolveDocument(app, item.name, out var doc))
                {
                    DiagnosticLog.WriteProductOperation(
                        "ExternalEvent." + item.name,
                        item.operationId,
                        "SKIPPED_DOCUMENT_MISMATCH",
                        _expectedDocument,
                        "ExternalEvent did not execute because the bound document was closed or not active.");
                    continue;
                }

                DiagnosticLog.WriteProductOperation(
                    "ExternalEvent." + item.name,
                    item.operationId,
                    "START",
                    doc,
                    _wrapInTransactionGroup ? "Path=Window→ExternalEvent→TransactionGroup→Action" : "Path=Window→ExternalEvent→Action");

                if (_wrapInTransactionGroup && doc != null && !doc.IsReadOnly)
                {
                    ExecuteWithTransactionGroup(app, doc, item.action, item.name, item.showSuccess && _showAutomaticSuccess, item.operationId);
                }
                else
                {
                    ExecuteWithoutTransactionGroup(app, doc, item.action, item.name, item.showSuccess && _showAutomaticSuccess, item.operationId);
                }
            }
        }

        private bool TryResolveDocument(UIApplication app, string operationName, out Document doc)
        {
            doc = app.ActiveUIDocument?.Document;

            if (_expectedDocument == null)
            {
                return true;
            }

            if (!_expectedDocument.IsValidObject)
            {
                TaskDialog.Show("文档已关闭", $"“{operationName}”未执行。此窗口绑定的 Revit 文档已经关闭，请关闭该窗口后重新打开功能。");
                return false;
            }

            if (doc == null || !doc.Equals(_expectedDocument))
            {
                TaskDialog.Show(
                    "当前文档已切换",
                    $"“{operationName}”未执行。此窗口绑定到“{_expectedDocument.Title}”。\n请切回该文档，或关闭窗口后在当前文档重新打开功能。");
                return false;
            }

            doc = _expectedDocument;
            return true;
        }

        private static void ExecuteWithoutTransactionGroup(
            UIApplication app,
            Document doc,
            Action<UIApplication> action,
            string operationName,
            bool showSuccess,
            string operationId)
        {
            try
            {
                action.Invoke(app);
                DiagnosticLog.WriteProductOperation(
                    "ExternalEvent." + operationName,
                    operationId,
                    "COMPLETE",
                    doc,
                    "Action completed without exception. Inner transaction/result details are logged by the business action when applicable.");
                if (showSuccess)
                {
                    TaskDialog.Show("成功", $"{operationName} 执行成功。");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "CANCELLED", doc, "Autodesk operation cancelled by user.");
            }
            catch (OperationCanceledException)
            {
                DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "CANCELLED", doc, "Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError(operationName, ex);
                DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "FAILED", doc, ex.ToString());
                TaskDialog.Show("错误", $"{operationName} 失败。\n错误信息：{ex.Message}");
            }
        }

        private static void ExecuteWithTransactionGroup(
            UIApplication app,
            Document doc,
            Action<UIApplication> action,
            string operationName,
            bool showSuccess,
            string operationId)
        {
            using (TransactionGroup tg = new TransactionGroup(doc, operationName))
            {
                try
                {
                    tg.Start();
                    action.Invoke(app);
                    TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                        tg,
                        "成功",
                        $"{operationName} 执行成功。",
                        app.ActiveUIDocument,
                        showSuccess);
                    if (commitStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException($"事务组未成功提交，状态：{commitStatus}");
                    DiagnosticLog.WriteProductOperation(
                        "ExternalEvent." + operationName,
                        operationId,
                        "COMMITTED",
                        doc,
                        $"TransactionGroupStatus={commitStatus}");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                    DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "CANCELLED_ROLLEDBACK", doc, "Autodesk operation cancelled.");
                }
                catch (OperationCanceledException)
                {
                    if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                    DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "CANCELLED_ROLLEDBACK", doc, "Operation cancelled.");
                }
                catch (Exception ex)
                {
                    if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                    DiagnosticLog.WriteError(operationName, ex);
                    DiagnosticLog.WriteProductOperation("ExternalEvent." + operationName, operationId, "FAILED_ROLLEDBACK", doc, ex.ToString());
                    TaskDialog.Show("错误", $"{operationName} 失败，已撤销。\n错误信息：{ex.Message}");
                }
            }
        }

        public string GetName() => "YangTools RevitEventHandler";
    }
}
