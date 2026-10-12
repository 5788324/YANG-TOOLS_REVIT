using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>Immutable request values captured at ComboBox DropDownClosed.</summary>
    internal sealed class ProjectAssetLinePatternRequest
    {
        public ElementId CategoryId { get; set; }
        public ElementId PatternId { get; set; }
    }

    internal sealed class ProjectAssetLinePatternResult
    {
        public bool Changed { get; internal set; }
        public ElementId PatternId { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; }
    }

    /// <summary>Only call from a Revit ExternalEvent on its UI thread; no WPF dependencies.</summary>
    internal static class ProjectAssetLinePatternService
    {
        internal static ProjectAssetLinePatternResult Execute(Document document, ProjectAssetLinePatternRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，线图案未修改。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目只读，不能更改线图案。");
            if (request == null || request.CategoryId == null || request.PatternId == null ||
                request.CategoryId.GetIdValue() == ElementId.InvalidElementId.GetIdValue() ||
                request.PatternId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("线样式或线图案 ID 无效，请刷新列表。");

            var lines = Category.GetCategory(document, BuiltInCategory.OST_Lines);
            var category = lines?.SubCategories.Cast<Category>()
                .FirstOrDefault(c => c.Id.GetIdValue() == request.CategoryId.GetIdValue());
            if (category == null)
                throw new InvalidOperationException("目标线样式已经不存在，请刷新列表。");

            var solidId = LinePatternElement.GetSolidPatternId();
            bool isSolid = request.PatternId.GetIdValue() == solidId.GetIdValue();
            if (!isSolid && !(document.GetElement(request.PatternId) is LinePatternElement))
                throw new InvalidOperationException("所选线图案已不存在，请刷新列表。");

            var oldId = category.GetLinePatternId(GraphicsStyleType.Projection);
            if (oldId == null || oldId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                oldId = solidId;
            if (oldId.GetIdValue() == request.PatternId.GetIdValue())
                return new ProjectAssetLinePatternResult { Changed = false, PatternId = oldId, ReadbackConfirmed = true };

            using (var transaction = new Transaction(document, "更改线型图案"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动线图案事务，模型未修改。");
                try
                {
                    category.SetLinePatternId(request.PatternId, GraphicsStyleType.Projection);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("线图案事务未确认提交，请检查 Revit 状态。");
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
                                $"线图案修改失败且回滚结果无法确认：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw new InvalidOperationException("线图案未确认成功提交：" + ex.Message, ex);
                }
            }

            // Commit has already succeeded. A failed/mismatched readback is NOT a failed
            // write: report its own status rather than prompting the user to retry.
            try
            {
                var actual = category.GetLinePatternId(GraphicsStyleType.Projection);
                if (actual == null || actual.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                    actual = solidId;
                if (actual.GetIdValue() != request.PatternId.GetIdValue())
                    return new ProjectAssetLinePatternResult
                    {
                        Changed = true, PatternId = actual, ReadbackConfirmed = false,
                        ReadbackWarning = "线图案已提交，但回读值与目标不一致，请刷新数据核对。"
                    };
                return new ProjectAssetLinePatternResult
                {
                    Changed = true, PatternId = actual, ReadbackConfirmed = true
                };
            }
            catch (Exception ex)
            {
                return new ProjectAssetLinePatternResult
                {
                    Changed = true, PatternId = request.PatternId, ReadbackConfirmed = false,
                    ReadbackWarning = "线图案已提交，但模型回读失败：" + ex.Message
                };
            }
        }
    }
}
