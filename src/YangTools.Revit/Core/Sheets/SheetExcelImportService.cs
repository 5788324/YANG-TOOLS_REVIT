using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.Sheets
{
    internal sealed class SheetExcelImportRequest
    {
        // Snapshot of spreadsheet values. No WPF controls or live Excel reader retained.
        public IReadOnlyList<IDictionary<string, object>> Rows { get; set; }
    }

    internal sealed class SheetExcelImportResult
    {
        public bool Committed { get; internal set; }
        public int CreatedCount { get; internal set; }
        public int UpdatedCount { get; internal set; }
        public int SkippedBlankRows { get; internal set; }
        public bool SheetIdentityReadbackConfirmed { get; internal set; }
    }

    /// <summary>
    /// All-or-nothing sheet Excel import on Revit's ExternalEvent main thread.
    /// Any nonempty, invalid row, missing/readonly parameter or failed Set
    /// rejects the entire batch. No hidden partial success / one Ctrl+Z.
    /// </summary>
    internal static class SheetExcelImportService
    {
        private const string SheetNumberColumn = "图号 (Sheet Number)";
        private const string SheetNameColumn = "图名 (Sheet Name)";

        private sealed class RowPlan
        {
            internal int ExcelRow { get; set; }
            internal string SheetNumber { get; set; } = string.Empty;
            internal string SheetName { get; set; } = string.Empty;
            internal ViewSheet ExistingSheet { get; set; }
            internal IDictionary<string, object> Data { get; set; }
        }

        internal static SheetExcelImportResult Execute(Document doc, SheetExcelImportRequest request)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("项目文档无效、只读或存在未完成事务，图纸未导入。");
            if (request?.Rows == null)
                throw new InvalidOperationException("Excel 内容未加载，图纸未导入。");

            var result = new SheetExcelImportResult();
            var sheetsByNumber = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .GroupBy(s => s.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plans = new List<RowPlan>();
            for (int i = 0; i < request.Rows.Count; i++)
            {
                var row = request.Rows[i];
                if (row == null || row.All(k => k.Value == null || string.IsNullOrWhiteSpace(k.Value.ToString())))
                {
                    result.SkippedBlankRows++;
                    continue;
                }
                if (!row.TryGetValue(SheetNumberColumn, out object sheetNumValue) ||
                    string.IsNullOrWhiteSpace(sheetNumValue?.ToString()))
                    throw new InvalidOperationException($"Excel 第 {i + 2} 行缺少图号；整个批次未修改模型。");
                string number = sheetNumValue.ToString().Trim();
                if (!seenNumbers.Add(number))
                    throw new InvalidOperationException($"Excel 中重复图号“{number}”；整个批次未修改模型。");
                sheetsByNumber.TryGetValue(number, out List<ViewSheet> matches);
                if (matches != null && matches.Count > 1)
                    throw new InvalidOperationException($"项目中有多张同图号“{number}”图纸，无法唯一定位；整个批次未修改模型。");
                ViewSheet sheet = matches?.FirstOrDefault();
                if (sheet != null)
                {
                    // Read-only preflight of ALL explicitly populated extra fields.
                    foreach (var field in row)
                    {
                        if (IsReservedOrEmpty(field)) continue;
                        Parameter parameter = ResolveUniqueWritableParameter(sheet, field.Key, i + 2);
                        if (parameter.IsReadOnly)
                            throw new InvalidOperationException($"Excel 第 {i + 2} 行参数“{field.Key}”只读；已取消整批导入。");
                    }
                }
                plans.Add(new RowPlan
                {
                    ExcelRow = i + 2,
                    SheetNumber = number,
                    SheetName = row.TryGetValue(SheetNameColumn, out object sheetName) ?
                        sheetName?.ToString()?.Trim() ?? string.Empty : string.Empty,
                    ExistingSheet = sheet,
                    Data = row
                });
            }
            if (plans.Count == 0) return result; // No transaction or Undo for blank input.

            ElementId titleblockId = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Select(t => t.Id).FirstOrDefault() ?? ElementId.InvalidElementId;
            var written = new List<KeyValuePair<ElementId, RowPlan>>();
            using (var transaction = new Transaction(doc, "批量导入图纸（整批）"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动图纸导入事务，模型未修改。");
                try
                {
                    foreach (var plan in plans)
                    {
                        ViewSheet sheet = plan.ExistingSheet;
                        if (sheet == null)
                        {
                            sheet = ViewSheet.Create(doc, titleblockId);
                            if (sheet == null) throw new InvalidOperationException($"第 {plan.ExcelRow} 行无法创建图纸。");
                            sheet.SheetNumber = plan.SheetNumber;
                            sheet.Name = string.IsNullOrWhiteSpace(plan.SheetName) ? "未命名图纸" : plan.SheetName;
                            result.CreatedCount++;
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(plan.SheetName) && sheet.Name != plan.SheetName)
                                sheet.Name = plan.SheetName;
                            result.UpdatedCount++;
                        }
                        foreach (var field in plan.Data)
                        {
                            if (IsReservedOrEmpty(field)) continue;
                            Parameter parameter = ResolveUniqueWritableParameter(sheet, field.Key, plan.ExcelRow);
                            if (parameter.IsReadOnly)
                                throw new InvalidOperationException($"第 {plan.ExcelRow} 行参数“{field.Key}”为只读。");
                            if (!ParameterValueHelper.TrySetFromDisplayValue(
                                parameter, field.Value.ToString(), out string error))
                                throw new InvalidOperationException($"第 {plan.ExcelRow} 行参数“{field.Key}”写入失败：{error}");
                        }
                        written.Add(new KeyValuePair<ElementId, RowPlan>(sheet.Id, plan));
                    }
                    // Still in the transaction: missing targets must abort all changes.
                    foreach (var pair in written)
                    {
                        var actual = doc.GetElement(pair.Key) as ViewSheet;
                        if (actual == null || !string.Equals(actual.SheetNumber, pair.Value.SheetNumber, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(pair.Value.SheetName) && actual.Name != pair.Value.SheetName))
                            throw new InvalidOperationException($"第 {pair.Value.ExcelRow} 行图纸身份/名称回读不一致，取消整批导入。");
                    }
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"图纸导入提交状态未确认：{status}，请检查模型。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚没有返回 RolledBack。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"图纸导入失败且回滚结果未知：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw;
                }
            }
            result.Committed = true;
            try
            {
                result.SheetIdentityReadbackConfirmed = written.All(pair =>
                {
                    var actual = doc.GetElement(pair.Key) as ViewSheet;
                    return actual != null &&
                        string.Equals(actual.SheetNumber, pair.Value.SheetNumber, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(pair.Value.SheetName) || actual.Name == pair.Value.SheetName);
                });
            }
            catch
            {
                // A post-commit readback failure cannot roll back an accepted commit.
                result.SheetIdentityReadbackConfirmed = false;
            }
            return result;
        }

        private static Parameter ResolveUniqueWritableParameter(ViewSheet sheet, string key, int excelRow)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"Excel 第 {excelRow} 行存在无名称的参数列。");
            var matches = sheet.GetParameters(key);
            if (matches == null || matches.Count == 0)
                throw new InvalidOperationException($"Excel 第 {excelRow} 行参数“{key}”不存在；已取消整批导入。");
            if (matches.Count != 1)
                throw new InvalidOperationException($"Excel 第 {excelRow} 行参数“{key}”存在重复同名项，无法唯一定位；未修改模型。");
            return matches[0];
        }

        private static bool IsReservedOrEmpty(KeyValuePair<string, object> field)
        {
            return field.Key == SheetNumberColumn || field.Key == SheetNameColumn || field.Value == null;
        }
    }
}
