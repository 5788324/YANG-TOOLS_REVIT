using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>File rows are copied by the UI before scheduling the ExternalEvent.</summary>
    internal sealed class ProjectAssetExcelImportRequest
    {
        public IReadOnlyList<IDictionary<string, object>> Rows { get; set; } =
            Array.Empty<IDictionary<string, object>>();
    }

    internal sealed class ProjectAssetExcelImportResult
    {
        public int ChangedCount { get; internal set; }
        public int UnchangedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Executes only on Revit's API thread inside ExternalEvent.
    /// A TransactionGroup unifies all committed row transactions into one Undo.
    /// On any actual write failure, rollback the entire group rather than committing partial rows.
    /// </summary>
    internal static class ProjectAssetExcelImportService
    {
        private sealed class ImportRow
        {
            internal int ExcelLine { get; set; }
            internal long ElementIdValue { get; set; }
            internal Element Element { get; set; }
            internal IDictionary<string, object> Cells { get; set; }
        }

        internal static ProjectAssetExcelImportResult Execute(Document document, ProjectAssetExcelImportRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，Excel 导入未执行。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目为只读，Excel 导入未执行。");
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new ProjectAssetExcelImportResult();
            var input = request.Rows ?? Array.Empty<IDictionary<string, object>>();
            var plan = new List<ImportRow>();
            var seenIds = new HashSet<long>();
            for (int i = 0; i < input.Count; i++)
            {
                int line = i + 2; // Excel header is row 1.
                var cells = input[i];
                if (cells == null || !TryGetAssetId(cells, out long idValue) || idValue <= 0)
                {
                    result.SkippedCount++;
                    result.Warnings.Add($"第 {line} 行没有有效图元 ID，未导入。");
                    continue;
                }
                if (!seenIds.Add(idValue))
                    throw new InvalidOperationException($"第 {line} 行图元 ID {idValue} 重复，整批导入未执行。请合并重复行。");
                Element element = document.GetElement(ElementIdExtensions.CreateId(idValue));
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"第 {line} 行图元 ID {idValue} 已不存在，整批导入未执行。");
                plan.Add(new ImportRow { ExcelLine = line, ElementIdValue = idValue, Element = element, Cells = cells });
            }
            if (plan.Count == 0) return result; // No transaction, no Undo.

            using (var group = new TransactionGroup(document, "导入资产 Excel"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动资产导入事务组，项目未修改。");
                try
                {
                    foreach (ImportRow row in plan)
                    {
                        using (var transaction = new Transaction(document, $"导入资产：{row.ElementIdValue}"))
                        {
                            if (transaction.Start() != TransactionStatus.Started)
                                throw new InvalidOperationException($"第 {row.ExcelLine} 行无法启动事务。");
                            try
                            {
                                bool changed = ApplyRow(row);
                                if (!changed)
                                {
                                    if (transaction.RollBack() != TransactionStatus.RolledBack)
                                        throw new InvalidOperationException("未修改行的临时事务回滚失败。");
                                    result.UnchangedCount++;
                                    continue;
                                }
                                if (transaction.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("该行事务提交失败。");
                                result.ChangedCount++;
                            }
                            catch (Exception ex)
                            {
                                if (transaction.GetStatus() == TransactionStatus.Started)
                                    transaction.RollBack();
                                throw new InvalidOperationException(
                                    $"第 {row.ExcelLine} 行图元 ID {row.ElementIdValue}：{ex.Message}", ex);
                            }
                        }
                    }

                    if (result.ChangedCount == 0)
                    {
                        if (group.RollBack() != TransactionStatus.RolledBack)
                            throw new InvalidOperationException("空导入事务组回滚失败，请检查项目状态。");
                    }
                    else if (group.Assimilate() != TransactionStatus.Committed)
                    {
                        throw new InvalidOperationException("导入资产事务组合并失败，请检查 Revit 事务状态。");
                    }
                }
                catch (Exception ex)
                {
                    bool rollbackConfirmed = group.GetStatus() == TransactionStatus.RolledBack;
                    if (group.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            rollbackConfirmed = group.RollBack() == TransactionStatus.RolledBack;
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                "Excel 导入失败，整批回滚未能确认：" + rollbackError.Message, ex);
                        }
                    }
                    if (!rollbackConfirmed)
                        throw new InvalidOperationException(
                            "Excel 导入失败，事务状态无法确认。请检查项目更改后再决定是否重试：" + ex.Message, ex);
                    throw new InvalidOperationException("Excel 导入失败，整批事务已回滚：" + ex.Message, ex);
                }
            }
            return result;
        }

        private static bool ApplyRow(ImportRow row)
        {
            bool changed = false;
            if (TryGetAssetName(row.Cells, out string newName) &&
                !string.IsNullOrWhiteSpace(newName) &&
                !string.Equals(row.Element.Name, newName, StringComparison.Ordinal))
            {
                row.Element.Name = newName;
                changed = true;
            }

            foreach (KeyValuePair<string, object> cell in row.Cells)
            {
                if (IsAssetIdentityColumn(cell.Key)) continue;
                string parameterName = cell.Key;
                string parameterValue = cell.Value?.ToString() ?? string.Empty;
                Parameter parameter = row.Element.LookupParameter(parameterName) ??
                    row.Element.GetOrderedParameters()
                        .FirstOrDefault(p => p.Definition?.Name == parameterName);
                // Never silently discard a column whose content was intended as a write.
                if (parameter == null)
                    throw new InvalidOperationException($"找不到参数“{parameterName}”，未忽略该列。");
                string oldValue = ParameterValueHelper.GetDisplayValue(parameter);
                if (oldValue == parameterValue) continue;
                if (parameter.IsReadOnly)
                    throw new InvalidOperationException($"参数“{parameterName}”是只读项，未忽略该列。");
                if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, parameterValue, out string error))
                    throw new InvalidOperationException($"参数“{parameterName}”写入失败：{error}");
                changed = true;
            }
            return changed;
        }

        private static bool TryGetAssetId(IDictionary<string, object> row, out long idValue)
        {
            idValue = 0;
            object value;
            if (!row.TryGetValue("图元 ID", out value) &&
                !row.TryGetValue("图元ID", out value) &&
                !row.TryGetValue("元素ID", out value)) return false;
            return value != null && long.TryParse(value.ToString(), out idValue);
        }

        private static bool TryGetAssetName(IDictionary<string, object> row, out string name)
        {
            name = string.Empty;
            object value;
            if (!row.TryGetValue("名称", out value) &&
                !row.TryGetValue("资产名称", out value)) return false;
            name = value?.ToString()?.Trim() ?? string.Empty;
            return true;
        }

        private static bool IsAssetIdentityColumn(string column)
        {
            return column == "图元 ID" || column == "图元ID" || column == "元素ID" ||
                column == "名称" || column == "资产名称";
        }
    }
}
