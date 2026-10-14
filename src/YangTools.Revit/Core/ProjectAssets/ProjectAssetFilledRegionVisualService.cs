using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    internal enum ProjectAssetFilledRegionVisualKind
    {
        ForegroundPattern, BackgroundPattern, ForegroundColor, BackgroundColor, Duplicate
    }

    internal sealed class ProjectAssetFilledRegionVisualRequest
    {
        public ElementId TypeId { get; set; }
        public ProjectAssetFilledRegionVisualKind Kind { get; set; }
        public ElementId PatternId { get; set; }
        public Autodesk.Revit.DB.Color Color { get; set; }
    }

    internal sealed class ProjectAssetFilledRegionVisualResult
    {
        public bool Changed { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public ElementId ResultTypeId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
    }

    /// <summary>Revit main-thread, document-bound service; no WPF references.</summary>
    internal static class ProjectAssetFilledRegionVisualService
    {
        internal static ProjectAssetFilledRegionVisualResult Execute(Document doc, ProjectAssetFilledRegionVisualRequest request)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("文档无效、只读或正在修改，填充区域未编辑。");
            if (request?.TypeId == null || request.TypeId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("没有有效的填充区域类型，请刷新列表。");
            var source = doc.GetElement(request.TypeId) as FilledRegionType;
            if (source == null || !source.IsValidObject)
                throw new InvalidOperationException("填充区域类型已不存在，请刷新列表。");

            bool isPattern = request.Kind == ProjectAssetFilledRegionVisualKind.ForegroundPattern ||
                request.Kind == ProjectAssetFilledRegionVisualKind.BackgroundPattern;
            bool isColor = request.Kind == ProjectAssetFilledRegionVisualKind.ForegroundColor ||
                request.Kind == ProjectAssetFilledRegionVisualKind.BackgroundColor;
            bool isDuplicate = request.Kind == ProjectAssetFilledRegionVisualKind.Duplicate;
            if (!isPattern && !isColor && !isDuplicate)
                throw new InvalidOperationException("不支持的填充区域编辑类型。");

            ElementId expectedPattern = request.PatternId;
            Autodesk.Revit.DB.Color expectedColor = request.Color;
            if (isPattern)
            {
                if (expectedPattern == null)
                    throw new InvalidOperationException("填充图案尚未选择。");
                if (expectedPattern.GetIdValue() != ElementId.InvalidElementId.GetIdValue() &&
                    !(doc.GetElement(expectedPattern) is FillPatternElement))
                    throw new InvalidOperationException("目标填充图案不存在或不是有效图案，请重新选择。");
                ElementId oldPattern = request.Kind == ProjectAssetFilledRegionVisualKind.ForegroundPattern
                    ? source.ForegroundPatternId : source.BackgroundPatternId;
                if (oldPattern != null && oldPattern.GetIdValue() == expectedPattern.GetIdValue())
                    return new ProjectAssetFilledRegionVisualResult
                    { Changed = false, ReadbackConfirmed = true, ResultTypeId = source.Id, Name = source.Name };
            }
            if (isColor)
            {
                if (expectedColor == null)
                    throw new InvalidOperationException("尚未选择填充颜色。");
                Autodesk.Revit.DB.Color oldColor = request.Kind == ProjectAssetFilledRegionVisualKind.ForegroundColor
                    ? source.ForegroundPatternColor : source.BackgroundPatternColor;
                if (SameColor(oldColor, expectedColor))
                    return new ProjectAssetFilledRegionVisualResult
                    { Changed = false, ReadbackConfirmed = true, ResultTypeId = source.Id, Name = source.Name };
            }

            string newName = string.Empty;
            if (isDuplicate)
            {
                var names = new HashSet<string>(new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().Select(x => x.Name),
                    StringComparer.OrdinalIgnoreCase);
                int i = 1;
                do { newName = source.Name + " copy " + i++; }
                while (names.Contains(newName));
            }

            ElementId targetId = source.Id;
            using (var transaction = new Transaction(doc, isDuplicate ? "复制填充区域" : "修改填充区域样式"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("填充区域事务无法启动，模型未修改。");
                try
                {
                    switch (request.Kind)
                    {
                        case ProjectAssetFilledRegionVisualKind.ForegroundPattern:
                            source.ForegroundPatternId = expectedPattern; break;
                        case ProjectAssetFilledRegionVisualKind.BackgroundPattern:
                            source.BackgroundPatternId = expectedPattern; break;
                        case ProjectAssetFilledRegionVisualKind.ForegroundColor:
                            source.ForegroundPatternColor = expectedColor; break;
                        case ProjectAssetFilledRegionVisualKind.BackgroundColor:
                            source.BackgroundPatternColor = expectedColor; break;
                        case ProjectAssetFilledRegionVisualKind.Duplicate:
                            ElementType duplicate = source.Duplicate(newName);
                            if (duplicate == null || duplicate.Id == ElementId.InvalidElementId)
                                throw new InvalidOperationException("复制填充区域失败，未得到新类型。");
                            targetId = duplicate.Id;
                            break;
                    }
                    var verify = doc.GetElement(targetId) as FilledRegionType;
                    if (!Matches(verify, request, newName))
                        throw new InvalidOperationException("填充区域提交前读回不一致，已取消修改。");
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"填充区域事务提交未确认：{status}，请核查模型状态。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚未确认。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"填充区域修改失败且回滚未知：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw;
                }
            }
            var actual = doc.GetElement(targetId) as FilledRegionType;
            return new ProjectAssetFilledRegionVisualResult
            {
                Changed = true,
                ResultTypeId = targetId,
                Name = actual?.Name ?? newName,
                ReadbackConfirmed = Matches(actual, request, newName)
            };
        }

        private static bool Matches(FilledRegionType type, ProjectAssetFilledRegionVisualRequest request, string name)
        {
            if (type == null || !type.IsValidObject) return false;
            switch (request.Kind)
            {
                case ProjectAssetFilledRegionVisualKind.ForegroundPattern:
                    return type.ForegroundPatternId.GetIdValue() == request.PatternId.GetIdValue();
                case ProjectAssetFilledRegionVisualKind.BackgroundPattern:
                    return type.BackgroundPatternId.GetIdValue() == request.PatternId.GetIdValue();
                case ProjectAssetFilledRegionVisualKind.ForegroundColor:
                    return SameColor(type.ForegroundPatternColor, request.Color);
                case ProjectAssetFilledRegionVisualKind.BackgroundColor:
                    return SameColor(type.BackgroundPatternColor, request.Color);
                case ProjectAssetFilledRegionVisualKind.Duplicate:
                    return type.Name == name && type.Id.GetIdValue() != request.TypeId.GetIdValue();
                default: return false;
            }
        }

        private static bool SameColor(Autodesk.Revit.DB.Color x, Autodesk.Revit.DB.Color y)
        {
            return x != null && y != null && x.Red == y.Red && x.Green == y.Green && x.Blue == y.Blue;
        }
    }
}
