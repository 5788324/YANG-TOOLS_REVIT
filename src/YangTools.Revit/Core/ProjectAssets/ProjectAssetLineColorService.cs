using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>Immutable color values captured before the modeless ExternalEvent is queued.</summary>
    internal sealed class ProjectAssetLineColorRequest
    {
        public ElementId CategoryId { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }

    internal sealed class ProjectAssetLineColorResult
    {
        public bool Changed { get; internal set; }
        public byte Red { get; internal set; }
        public byte Green { get; internal set; }
        public byte Blue { get; internal set; }
    }

    /// <summary>
    /// Runs on the Revit UI thread from ExternalEvent. Categories including negative
    /// built-in identifiers are resolved from OST_Lines.SubCategories, not GetElement.
    /// No WPF controls or ViewModels cross this boundary.
    /// </summary>
    internal static class ProjectAssetLineColorService
    {
        internal static ProjectAssetLineColorResult Execute(Document document, ProjectAssetLineColorRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，线颜色未修改。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("当前项目只读，线颜色不能修改。");
            if (request == null || request.CategoryId == null ||
                request.CategoryId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("线样式 ID 无效，请刷新列表。");

            var lines = Category.GetCategory(document, BuiltInCategory.OST_Lines);
            var category = lines?.SubCategories.Cast<Category>()
                .FirstOrDefault(c => c.Id.GetIdValue() == request.CategoryId.GetIdValue());
            if (category == null)
                throw new InvalidOperationException("线样式不存在，请刷新列表后重试。");

            var oldColor = category.LineColor;
            if (oldColor != null && oldColor.Red == request.Red &&
                oldColor.Green == request.Green && oldColor.Blue == request.Blue)
            {
                return new ProjectAssetLineColorResult
                {
                    Changed = false,
                    Red = oldColor.Red, Green = oldColor.Green, Blue = oldColor.Blue
                };
            }

            using (var transaction = new Transaction(document, "更改线颜色"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动线颜色事务，未写入修改。");
                try
                {
                    category.LineColor = new Autodesk.Revit.DB.Color(request.Red, request.Green, request.Blue);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("线颜色事务未确认提交，请检查 Revit 状态。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"线颜色保存异常且回滚结果无法确认：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw new InvalidOperationException("线颜色未确认成功提交：" + ex.Message, ex);
                }
            }

            // Read back the persisted Revit category property only after a confirmed commit.
            var actual = category.LineColor;
            return new ProjectAssetLineColorResult
            {
                Changed = true,
                Red = actual.Red, Green = actual.Green, Blue = actual.Blue
            };
        }
    }
}
