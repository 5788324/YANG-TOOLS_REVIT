using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    internal sealed class FamilyInstanceExcelImportRequest
    {
        public IReadOnlyList<IDictionary<string, object>> Rows { get; set; }
    }

    internal sealed class FamilyInstanceExcelImportResult
    {
        public bool Committed { get; internal set; }
        public int ChangedElements { get; internal set; }
        public int WrittenParameters { get; internal set; }
        public int SkippedBlankRows { get; internal set; }
        public int SkippedMetadataOnlyRows { get; internal set; }
        public int SkippedUnchangedRows { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Document-bound, ExternalEvent-only Excel import. Preflight ALL populated cells;
    /// one transaction and one Undo, never partially accept a failed batch.
    /// </summary>
    internal static class FamilyInstanceExcelImportService
    {
        private static readonly HashSet<string> ReadOnlyExcelColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "图元ID", "类型 (Type)", "标高", "主体", "固定"
        };

        private sealed class ParameterPlan
        {
            internal string Label;
            internal string Value;
            internal Parameter Parameter;
            internal object AppliedValue;
        }

        private sealed class RowPlan
        {
            internal int RowNumber;
            internal ElementId ElementId;
            internal List<ParameterPlan> Parameters = new List<ParameterPlan>();
        }

        internal static FamilyInstanceExcelImportResult Execute(Document doc, FamilyInstanceExcelImportRequest request)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("文档已关闭、只读或正在进行其他事务，实例参数未导入。");
            if (request?.Rows == null)
                throw new InvalidOperationException("Excel 没有可读取的内容，实例参数未导入。");

            var result = new FamilyInstanceExcelImportResult { ReadbackConfirmed = true };
            var seenIds = new HashSet<long>();
            var plans = new List<RowPlan>();
            for (int rowIndex = 0; rowIndex < request.Rows.Count; rowIndex++)
            {
                IDictionary<string, object> row = request.Rows[rowIndex];
                int excelRow = rowIndex + 2;
                if (row == null || row.All(x => x.Value == null || string.IsNullOrWhiteSpace(x.Value.ToString())))
                {
                    result.SkippedBlankRows++;
                    continue;
                }
                if (!row.TryGetValue("图元ID", out object idRaw) || idRaw == null ||
                    !long.TryParse(idRaw.ToString().Trim(), out long idValue) || idValue <= 0)
                    throw new InvalidOperationException($"Excel 第 {excelRow} 行图元ID无效，整个批次未修改模型。");
                if (!seenIds.Add(idValue))
                    throw new InvalidOperationException($"Excel 图元ID {idValue} 重复，整个批次未修改模型。");
                ElementId elementId = ElementIdExtensions.CreateId(idValue);
                Element element = doc.GetElement(elementId);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"Excel 第 {excelRow} 行找不到图元 {idValue}，整个批次未修改模型。");
                var plan = new RowPlan { RowNumber = excelRow, ElementId = elementId };
                var parameterIds = new HashSet<string>(StringComparer.Ordinal);
                bool hasImportedCells = false;
                foreach (KeyValuePair<string, object> cell in row)
                {
                    if (ReadOnlyExcelColumns.Contains(cell.Key) || cell.Value == null) continue;
                    if (string.IsNullOrWhiteSpace(cell.Key))
                        throw new InvalidOperationException($"Excel 第 {excelRow} 行有无标题的参数列，已取消整批导入。");
                    Parameter parameter = StableParameterKey.ResolveByDisplayLabel(element, cell.Key);
                    if (parameter == null)
                        throw new InvalidOperationException($"Excel 第 {excelRow} 行参数“{cell.Key}”不存在或同名不唯一，已取消整批导入。");
                    if (!ParameterValueHelper.CanEditAsText(parameter))
                        throw new InvalidOperationException($"Excel 第 {excelRow} 行参数“{cell.Key}”为只读或不支持文本写入，已取消整批导入。");
                    string stableId = StableParameterKey.Create(parameter);
                    if (!parameterIds.Add(stableId))
                        throw new InvalidOperationException($"Excel 第 {excelRow} 行多个列映射到同一个参数“{cell.Key}”，已取消整批导入。");
                    hasImportedCells = true;
                    if (string.Equals(ParameterValueHelper.GetDisplayValue(parameter), cell.Value.ToString(), StringComparison.Ordinal))
                        continue; // Unchanged values must not create an artificial Undo step.
                    plan.Parameters.Add(new ParameterPlan
                    {
                        Label = cell.Key,
                        Value = cell.Value.ToString() ?? string.Empty,
                        Parameter = parameter
                    });
                }
                if (plan.Parameters.Count == 0)
                {
                    if (hasImportedCells) result.SkippedUnchangedRows++;
                    else result.SkippedMetadataOnlyRows++;
                    continue;
                }
                plans.Add(plan);
            }
            if (plans.Count == 0) return result;

            using (var transaction = new Transaction(doc, "实例参数 Excel 整批导入"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动 Excel 整批导入事务，模型未修改。");
                try
                {
                    foreach (RowPlan row in plans)
                    {
                        Element element = doc.GetElement(row.ElementId);
                        if (element == null || !element.IsValidObject)
                            throw new InvalidOperationException($"第 {row.RowNumber} 行的图元在写入前已经失效。");
                        foreach (ParameterPlan parameter in row.Parameters)
                        {
                            if (!ParameterValueHelper.TrySetFromDisplayValue(parameter.Parameter, parameter.Value, out string setError))
                                throw new InvalidOperationException($"第 {row.RowNumber} 行参数“{parameter.Label}”写入失败：{setError}");
                            parameter.AppliedValue = ReadInternalValue(parameter.Parameter);
                            result.WrittenParameters++;
                        }
                        result.ChangedElements++;
                    }
                    // Reject lost/mismatched targets BEFORE committing; numerical display formatting
                    // is not compared here because Revit may normalize units/precision.
                    foreach (RowPlan row in plans)
                    {
                        if (doc.GetElement(row.ElementId) == null || row.Parameters.Any(p => p.Parameter == null))
                            throw new InvalidOperationException($"第 {row.RowNumber} 行的写入目标回读失效，取消整批导入。");
                    }
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Excel 整批导入提交状态未确认：{status}，模型状态可能未知。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚状态未确认。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"Excel 导入失败且回滚结果未知：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw;
                }
            }

            result.Committed = true;
            try
            {
                result.ReadbackConfirmed = plans.All(row =>
                {
                    Element element = doc.GetElement(row.ElementId);
                    if (element == null || !element.IsValidObject) return false;
                    return row.Parameters.All(p =>
                    {
                        Parameter actual = StableParameterKey.ResolveByDisplayLabel(element, p.Label);
                        return actual != null &&
                            InternalValuesEqual(ReadInternalValue(actual), p.AppliedValue);
                    });
                });
                if (!result.ReadbackConfirmed)
                    result.ReadbackWarning = "提交后部分参数的内部值与写入结果不一致，请检查对应实例。";
            }
            catch (Exception ex)
            {
                result.ReadbackConfirmed = false;
                result.ReadbackWarning = "模型已提交，但提交后的读取失败：" + ex.Message;
            }
            return result;
        }
        private static object ReadInternalValue(Parameter parameter)
        {
            switch (parameter.StorageType)
            {
                case StorageType.String: return parameter.AsString() ?? string.Empty;
                case StorageType.Integer: return parameter.AsInteger();
                case StorageType.Double: return parameter.AsDouble();
                default: throw new InvalidOperationException("Excel 导入不支持该参数的内部值读取。");
            }
        }

        private static bool InternalValuesEqual(object actual, object expected)
        {
            if (actual is double a && expected is double b)
                return Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Equals(actual, expected);
        }

    }
}
