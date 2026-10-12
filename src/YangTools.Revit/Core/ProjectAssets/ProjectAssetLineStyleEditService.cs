using System;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ProjectAssets
{
    internal enum ProjectAssetLineStyleEditKind
    {
        Name,
        LineWeight
    }

    /// <summary>Captured values only. Never retain a WPF control or ViewModel.</summary>
    internal sealed class ProjectAssetLineStyleEditRequest
    {
        public ProjectAssetLineStyleEditKind Kind { get; set; }
        public ElementId CategoryId { get; set; }
        public string NewName { get; set; } = string.Empty;
        public int LineWeight { get; set; }
    }

    internal sealed class ProjectAssetLineStyleEditResult
    {
        public bool Changed { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public int LineWeight { get; internal set; }
    }

    /// <summary>
    /// Revit main-thread service. One edit = at most one Transaction/Undo.
    /// Built-in line style category IDs may be negative and are intentionally
    /// resolved via OST_Lines.SubCategories rather than Document.GetElement.
    /// </summary>
    internal static class ProjectAssetLineStyleEditService
    {
        internal static ProjectAssetLineStyleEditResult Execute(
            Document document, ProjectAssetLineStyleEditRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，请重新打开项目资产管理器。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目为只读，不能修改线样式。");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.CategoryId == null ||
                request.CategoryId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("线样式 ID 无效，请刷新列表。");

            var lines = Category.GetCategory(document, BuiltInCategory.OST_Lines);
            var category = lines?.SubCategories.Cast<Category>()
                .FirstOrDefault(c => c.Id.GetIdValue() == request.CategoryId.GetIdValue());
            if (category == null)
                throw new InvalidOperationException("线样式已不存在，请刷新列表。");
            var result = new ProjectAssetLineStyleEditResult
            {
                Name = category.Name,
                LineWeight = category.GetLineWeight(GraphicsStyleType.Projection) ?? 1
            };
            if (request.Kind == ProjectAssetLineStyleEditKind.Name)
            {
                if (request.CategoryId.GetIdValue() < 0)
                    throw new InvalidOperationException("内置线样式不能重命名。");
                var newName = (request.NewName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newName))
                    throw new InvalidOperationException("线样式名称不能为空。");
                if (string.Equals(newName, result.Name, StringComparison.Ordinal))
                    return result;
            }
            else if (request.Kind == ProjectAssetLineStyleEditKind.LineWeight)
            {
                if (request.LineWeight < 1 || request.LineWeight > 16)
                    throw new InvalidOperationException("线宽必须为 1 至 16 的整数。");
                if (request.LineWeight == result.LineWeight)
                    return result;
            }
            else throw new InvalidOperationException("不支持的线样式编辑类型。");

            using (var transaction = new Transaction(document,
                request.Kind == ProjectAssetLineStyleEditKind.Name ? "重命名线样式" : "修改线宽"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动线样式编辑事务，模型未修改。");
                try
                {
                    if (request.Kind == ProjectAssetLineStyleEditKind.Name)
                    {
                        var graphicsStyle = category.GetGraphicsStyle(GraphicsStyleType.Projection);
                        if (graphicsStyle == null)
                            throw new InvalidOperationException("找不到线样式图形对象。");
                        graphicsStyle.Name = request.NewName.Trim();
                    }
                    else
                        category.SetLineWeight(request.LineWeight, GraphicsStyleType.Projection);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("线样式修改未确认提交，请检查 Revit 事务状态。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"线样式编辑失败且回滚结果无法确认：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw new InvalidOperationException("线样式编辑未成功提交：" + ex.Message, ex);
                }
            }
            result.Changed = true;
            result.Name = category.Name;
            result.LineWeight = category.GetLineWeight(GraphicsStyleType.Projection) ?? 1;
            return result;
        }
    }
}
