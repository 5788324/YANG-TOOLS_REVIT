using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    internal sealed class FamilyTypeExcelImportResult
    {
        internal bool Committed { get; set; }
        internal int ChangedTypes { get; set; }
        internal int WrittenParameters { get; set; }
        internal int SkippedBlankRows { get; set; }
        internal int SkippedUnchangedRows { get; set; }
        internal bool ReadbackConfirmed { get; set; } = true;
        internal string ReadbackWarning { get; set; } = string.Empty;
    }

    /// <summary>
    /// Imports exported FamilyManager Excel rows for exactly one family. All populated
    /// cells are checked before opening ONE transaction; failures roll back the batch.
    /// Must be invoked on the Revit API thread, never directly from the WPF UI thread.
    /// </summary>
    internal static class FamilyTypeExcelImportService
    {
        private sealed class ParameterPlan
        {
            internal string Label;
            internal string Value;
            internal Parameter Parameter;
            internal object AppliedValue;
        }

        private sealed class TypePlan
        {
            internal int Row;
            internal ElementId Id;
            internal string OriginalName;
            internal string RequestedName;
            internal List<ParameterPlan> Parameters = new List<ParameterPlan>();
            internal bool HasChanges => RequestedName != null || Parameters.Count != 0;
        }

        internal static FamilyTypeExcelImportResult Execute(
            Document doc, ElementId expectedFamilyId, IReadOnlyList<IDictionary<string, object>> rows)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("文档已关闭、只读或有其他事务，族类型未导入。");
            var family = doc.GetElement(expectedFamilyId) as Family;
            if (family == null || !family.IsValidObject)
                throw new InvalidOperationException("选中的族已失效，族类型未导入。");
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException("Excel 没有可导入的族类型数据。");

            var result = new FamilyTypeExcelImportResult();
            var seenIds = new HashSet<long>();
            var plans = new List<TypePlan>();
            var requestedFinalNames = new Dictionary<long, string>();
            var familyTypeIds = family.GetFamilySymbolIds();
            var familyTypeIdSet = new HashSet<long>(familyTypeIds.Select(id => id.GetIdValue()));

            for (int index = 0; index < rows.Count; index++)
            {
                IDictionary<string, object> row = rows[index];
                int line = index + 2;
                if (row == null || row.All(cell => cell.Value == null || string.IsNullOrWhiteSpace(cell.Value.ToString())))
                {
                    result.SkippedBlankRows++;
                    continue;
                }
                if (!row.TryGetValue("图元ID", out object rawId) || rawId == null ||
                    !long.TryParse(rawId.ToString()?.Trim(), out long id) || id <= 0)
                    throw new InvalidOperationException($"第 {line} 行图元ID无效；整批未修改。");
                if (!seenIds.Add(id))
                    throw new InvalidOperationException($"第 {line} 行图元ID {id} 重复；整批未修改。");
                if (!familyTypeIdSet.Contains(id))
                    throw new InvalidOperationException($"第 {line} 行类型 {id} 不属于当前选中的族；整批未修改。");
                var type = doc.GetElement(ElementIdExtensions.CreateId(id)) as FamilySymbol;
                if (type == null || !type.IsValidObject)
                    throw new InvalidOperationException($"第 {line} 行族类型 {id} 已失效；整批未修改。");
                var plan = new TypePlan { Row = line, Id = type.Id, OriginalName = type.Name };
                if (row.TryGetValue("类型名称 (Type)", out object nameCell) && nameCell != null)
                {
                    string requestedName = nameCell.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(requestedName))
                        throw new InvalidOperationException($"第 {line} 行类型名称为空；整批未修改。");
                    if (!string.Equals(type.Name, requestedName, StringComparison.Ordinal))
                        plan.RequestedName = requestedName;
                }
                requestedFinalNames[id] = plan.RequestedName ?? plan.OriginalName;

                var parameterKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cell in row)
                {
                    if (cell.Key == "图元ID" || cell.Key == "类型名称 (Type)" || cell.Value == null)
                        continue;
                    if (string.IsNullOrWhiteSpace(cell.Key))
                        throw new InvalidOperationException($"第 {line} 行有空白参数列名；整批未修改。");
                    Parameter parameter = StableParameterKey.ResolveByDisplayLabel(type, cell.Key);
                    if (parameter == null)
                        throw new InvalidOperationException($"第 {line} 行参数“{cell.Key}”不存在或同名不唯一；整批未修改。");
                    if (!ParameterValueHelper.CanEditAsText(parameter))
                        throw new InvalidOperationException($"第 {line} 行参数“{cell.Key}”只读或不支持文本编辑；整批未修改。");
                    string identity = StableParameterKey.Create(parameter);
                    if (!parameterKeys.Add(identity))
                        throw new InvalidOperationException($"第 {line} 行多列映射到参数“{cell.Key}”；整批未修改。");
                    string value = cell.Value.ToString() ?? string.Empty;
                    if (string.Equals(ParameterValueHelper.GetDisplayValue(parameter), value, StringComparison.Ordinal))
                        continue;
                    plan.Parameters.Add(new ParameterPlan { Label = cell.Key, Value = value, Parameter = parameter });
                }
                if (!plan.HasChanges)
                    result.SkippedUnchangedRows++;
                else
                    plans.Add(plan);
            }

            // Reject collisions with other types of this family, even when the colliding
            // type is not present in Excel. Revit family names must remain unique.
            var actualFinalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ElementId typeId in familyTypeIds)
            {
                long id = typeId.GetIdValue();
                string finalName = requestedFinalNames.TryGetValue(id, out string name)
                    ? name : (doc.GetElement(typeId)?.Name ?? string.Empty);
                if (!actualFinalNames.Add(finalName))
                    throw new InvalidOperationException($"导入将产生重复的族类型名称“{finalName}”；整批未修改。");
            }
            if (plans.Count == 0) return result;

            using (var transaction = new Transaction(doc, "族类型 Excel 整批导入"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法开始族类型 Excel 事务，模型未修改。");
                try
                {
                    foreach (TypePlan plan in plans)
                    {
                        var type = doc.GetElement(plan.Id) as FamilySymbol;
                        if (type == null || !type.IsValidObject)
                            throw new InvalidOperationException($"第 {plan.Row} 行族类型在执行前失效。");
                        if (plan.RequestedName != null) type.Name = plan.RequestedName;
                        foreach (ParameterPlan parameter in plan.Parameters)
                        {
                            if (!ParameterValueHelper.TrySetFromDisplayValue(parameter.Parameter, parameter.Value, out string error))
                                throw new InvalidOperationException($"第 {plan.Row} 行参数“{parameter.Label}”写入失败：{error}");
                            parameter.AppliedValue = ReadInternalValue(parameter.Parameter);
                            result.WrittenParameters++;
                        }
                        result.ChangedTypes++;
                    }
                    // Check before commit so known mismatches remain rollback-safe.
                    foreach (TypePlan plan in plans)
                    {
                        var type = doc.GetElement(plan.Id) as FamilySymbol;
                        if (type == null || !type.IsValidObject ||
                            (plan.RequestedName != null && !string.Equals(type.Name, plan.RequestedName, StringComparison.Ordinal)) ||
                            plan.Parameters.Any(p => p.Parameter == null ||
                                !InternalEqual(ReadInternalValue(p.Parameter), p.AppliedValue)))
                            throw new InvalidOperationException($"第 {plan.Row} 行写入后核对失败，取消整个批次。");
                    }
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"事务提交状态未确认：{status}；模型状态可能未知。");
                }
                catch (Exception original)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚状态未确认。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"导入失败且回滚结果未知：{rollbackError.Message}；原错误：{original.Message}", original);
                        }
                    }
                    throw;
                }
            }
            result.Committed = true;
            try
            {
                result.ReadbackConfirmed = plans.All(plan =>
                {
                    var type = doc.GetElement(plan.Id) as FamilySymbol;
                    if (type == null || !type.IsValidObject) return false;
                    if (plan.RequestedName != null && !string.Equals(type.Name, plan.RequestedName, StringComparison.Ordinal))
                        return false;
                    return plan.Parameters.All(p =>
                    {
                        var actual = StableParameterKey.ResolveByDisplayLabel(type, p.Label);
                        return actual != null &&
                            InternalEqual(ReadInternalValue(actual), p.AppliedValue);
                    });
                });
                if (!result.ReadbackConfirmed)
                    result.ReadbackWarning = "事务已提交，但部分族类型或参数回读结果不一致。";
            }
            catch (Exception ex)
            {
                result.ReadbackConfirmed = false;
                result.ReadbackWarning = "事务已提交，但读回过程失败：" + ex.Message;
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
                default: throw new InvalidOperationException("该参数类型不支持内部值校验。");
            }
        }

        private static bool InternalEqual(object a, object b)
        {
            if (a is double x && b is double y)
                return Math.Abs(x - y) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
            return Equals(a, b);
        }
    }
}
