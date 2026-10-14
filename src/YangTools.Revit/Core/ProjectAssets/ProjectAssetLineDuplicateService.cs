using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    internal sealed class ProjectAssetLineDuplicateRequest
    {
        public ElementId SourceCategoryId { get; set; }
        public string SourceName { get; set; } = string.Empty;
    }

    internal sealed class ProjectAssetLineDuplicateResult
    {
        public ElementId CategoryId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public bool ReadbackConfirmed { get; internal set; }
    }

    /// <summary>
    /// Revit ExternalEvent/main-thread only. A copied line style is a subcategory of OST_Lines,
    /// so negative category identifiers must be resolved through SubCategories, not GetElement.
    /// </summary>
    internal static class ProjectAssetLineDuplicateService
    {
        internal static ProjectAssetLineDuplicateResult Execute(Document document, ProjectAssetLineDuplicateRequest request)
        {
            if (document == null || !document.IsValidObject || document.IsReadOnly || document.IsModifiable)
                throw new InvalidOperationException("项目已关闭、只读或正被修改，未复制线样式。");
            if (request?.SourceCategoryId == null ||
                request.SourceCategoryId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("没有有效的源线样式，请刷新列表。");

            var root = Category.GetCategory(document, BuiltInCategory.OST_Lines);
            var source = root?.SubCategories.Cast<Category>().FirstOrDefault(
                x => x.Id.GetIdValue() == request.SourceCategoryId.GetIdValue());
            if (source == null)
                throw new InvalidOperationException("源线样式已不存在，请刷新列表。");
            if (!string.IsNullOrWhiteSpace(request.SourceName) &&
                !string.Equals(request.SourceName, source.Name, StringComparison.Ordinal))
                throw new InvalidOperationException("源线样式名称已改变，请刷新列表后重新复制。");

            var names = new HashSet<string>(root.SubCategories.Cast<Category>().Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);
            string newName;
            int suffix = 1;
            do { newName = source.Name + " copy " + suffix++; }
            while (names.Contains(newName));

            var originalColor = source.LineColor;
            var originalWeight = source.GetLineWeight(GraphicsStyleType.Projection);
            var originalPattern = source.GetLinePatternId(GraphicsStyleType.Projection);
            if (originalColor == null)
                throw new InvalidOperationException("源线样式没有可读取的颜色，未执行复制。");
            ElementId createdId = ElementId.InvalidElementId;
            using (var tx = new Transaction(document, "复制线型"))
            {
                if (tx.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动复制线样式事务，未更改模型。");
                try
                {
                    var created = document.Settings.Categories.NewSubcategory(root, newName);
                    if (created == null)
                        throw new InvalidOperationException("Revit 没有创建出线样式。");
                    createdId = created.Id;
                    created.LineColor = new Color(originalColor.Red, originalColor.Green, originalColor.Blue);
                    if (originalWeight.HasValue)
                        created.SetLineWeight(originalWeight.Value, GraphicsStyleType.Projection);
                    if (originalPattern != null && originalPattern != ElementId.InvalidElementId)
                        created.SetLinePatternId(originalPattern, GraphicsStyleType.Projection);

                    if (!Matches(created, newName, originalColor, originalWeight, originalPattern))
                        throw new InvalidOperationException("复制线样式提交前读回不一致，已取消修改。");
                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("复制线样式的事务未确认提交，请检查模型状态。");
                }
                catch (Exception error)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (tx.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("线样式事务回滚未确认。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"线样式复制失败且回滚状态未知：{rollbackError.Message}；原始错误：{error.Message}", error);
                        }
                    }
                    throw;
                }
            }
            var saved = root.SubCategories.Cast<Category>().FirstOrDefault(x =>
                x.Id.GetIdValue() == createdId.GetIdValue());
            return new ProjectAssetLineDuplicateResult
            {
                CategoryId = createdId,
                Name = saved?.Name ?? newName,
                ReadbackConfirmed = Matches(saved, newName, originalColor, originalWeight, originalPattern)
            };
        }

        private static bool Matches(Category target, string name, Color color, int? weight, ElementId pattern)
        {
            if (target == null || !string.Equals(target.Name, name, StringComparison.Ordinal)) return false;
            var copied = target.LineColor;
            if (copied == null || copied.Red != color.Red || copied.Green != color.Green || copied.Blue != color.Blue)
                return false;
            if (weight.HasValue && target.GetLineWeight(GraphicsStyleType.Projection) != weight)
                return false;
            var copiedPattern = target.GetLinePatternId(GraphicsStyleType.Projection);
            return copiedPattern != null && pattern != null &&
                copiedPattern.GetIdValue() == pattern.GetIdValue();
        }
    }
}
