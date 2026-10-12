using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class TransactionHelper
    {
        /// <summary>
        /// 提交普通事务。只有 Revit 明确返回 Committed 时才显示成功。
        /// 调用方可忽略返回值；需要严谨结果判定时应检查返回状态。
        /// </summary>
        /// <param name="showDialog">是否弹出 TaskDialog。MCP 等无 UI 调用方应传 false。</param>
        public static TransactionStatus ShowSuccessAndCommit(
            Transaction t,
            string title,
            string message,
            UIDocument uidoc = null,
            bool showDialog = true)
        {
            if (t == null)
                return TransactionStatus.Uninitialized;

            if (t.GetStatus() != TransactionStatus.Started)
                return t.GetStatus();

            if (uidoc != null)
            {
                uidoc.Document.Regenerate();
                uidoc.RefreshActiveView();
            }

            TransactionStatus status;
            try
            {
                status = t.Commit();
            }
            catch
            {
                if (t.GetStatus() == TransactionStatus.Started)
                {
                    try { t.RollBack(); } catch { }
                }
                throw;
            }

            if (status == TransactionStatus.Committed)
            {
                if (showDialog)
                    TaskDialog.Show(title, message);
            }
            else if (showDialog)
            {
                TaskDialog.Show("提交未完成", $"操作没有成功提交。事务状态：{status}");
            }

            return status;
        }

        /// <summary>
        /// 提交 TransactionGroup。只有 Assimilate 明确返回 Committed 时才显示成功。
        /// </summary>
        /// <param name="showDialog">是否弹出 TaskDialog。MCP 等无 UI 调用方应传 false。</param>
        public static TransactionStatus ShowSuccessAndCommit(
            TransactionGroup tg,
            string title,
            string message,
            UIDocument uidoc = null,
            bool showDialog = true)
        {
            if (tg == null)
                return TransactionStatus.Uninitialized;

            if (tg.GetStatus() != TransactionStatus.Started)
                return tg.GetStatus();

            if (uidoc != null)
            {
                try
                {
                    uidoc.Document.Regenerate();
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    // 没有可 Regenerate 的普通事务时跳过。
                }
                uidoc.RefreshActiveView();
            }

            TransactionStatus status;
            try
            {
                status = tg.Assimilate();
            }
            catch
            {
                if (tg.GetStatus() == TransactionStatus.Started)
                {
                    try { tg.RollBack(); } catch { }
                }
                if (showDialog)
                    TaskDialog.Show("提交失败", "操作未提交，事务组已回滚。请查看诊断日志。");
                throw;
            }

            if (status == TransactionStatus.Committed)
            {
                if (showDialog)
                    TaskDialog.Show(title, message);
            }
            else
            {
                if (tg.GetStatus() == TransactionStatus.Started)
                {
                    try { tg.RollBack(); } catch { }
                }
                if (showDialog)
                    TaskDialog.Show("提交未完成", $"操作没有成功提交。事务组状态：{status}");
            }

            return status;
        }
    }
}
