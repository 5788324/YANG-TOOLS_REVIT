using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    internal sealed class FamilyCategoryChangeRequest
    {
        public IList<ElementId> FamilyIds { get; set; } = new List<ElementId>();
        public BuiltInCategory TargetCategory { get; set; } = BuiltInCategory.INVALID;
        public string TargetCategoryName { get; set; } = string.Empty;
    }

    internal sealed class FamilyCategoryChangeResult
    {
        public int RequestedCount { get; set; }
        public int ChangedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public IList<string> Messages { get; } = new List<string>();

        public string BuildSummary()
        {
            string summary = $"请求：{RequestedCount} 个族\n成功：{ChangedCount}\n跳过：{SkippedCount}\n失败：{FailedCount}";
            if (Messages.Count == 0)
                return summary;

            return summary + "\n\n明细：\n" + string.Join("\n", Messages.Take(12))
                + (Messages.Count > 12 ? $"\n…另有 {Messages.Count - 12} 条" : string.Empty);
        }
    }

    internal static class FamilyCategoryChangeService
    {
        private sealed class ParameterValueSnapshot
        {
            public StorageType StorageType { get; set; }
            public object Value { get; set; }
        }

        private sealed class ElementSafetySnapshot
        {
            public ElementId ElementId { get; set; }
            public string ElementLabel { get; set; } = string.Empty;
            public Dictionary<long, ParameterValueSnapshot> CustomValues { get; } = new Dictionary<long, ParameterValueSnapshot>();
        }

        private sealed class FamilySafetySnapshot
        {
            public ElementId FamilyId { get; set; }
            public HashSet<long> SymbolIds { get; } = new HashSet<long>();
            public IList<ElementSafetySnapshot> Elements { get; } = new List<ElementSafetySnapshot>();
        }

        private sealed class PostReloadValidationException : InvalidOperationException
        {
            public PostReloadValidationException(string message) : base(message) { }
        }

        internal static FamilyCategoryChangeResult Execute(Document projectDocument, FamilyCategoryChangeRequest request)
        {
            if (projectDocument == null) throw new ArgumentNullException(nameof(projectDocument));
            if (projectDocument.IsFamilyDocument) throw new InvalidOperationException("族类别修改只能在 Revit 项目文档中运行。");
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.TargetCategory == BuiltInCategory.INVALID) throw new InvalidOperationException("目标族类别无效。");

            var ids = (request.FamilyIds ?? new List<ElementId>())
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();

            if (ids.Count == 0) throw new InvalidOperationException("没有选择需要修改的族。");

            Dictionary<long, IList<ElementId>> instanceIndex = BuildInstanceIndex(projectDocument, ids);
            var result = new FamilyCategoryChangeResult { RequestedCount = ids.Count };

            using (var group = new TransactionGroup(projectDocument, "批量修改族类别"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动族类别修改事务组。");

                try
                {
                    foreach (ElementId familyId in ids)
                    {
                        long rawFamilyId = familyId.GetIdValue();
                        instanceIndex.TryGetValue(rawFamilyId, out IList<ElementId> instanceIds);
                        ChangeOne(projectDocument, familyId, instanceIds ?? new List<ElementId>(), request, result);
                    }

                    TransactionStatus status = group.Assimilate();
                    if (status != TransactionStatus.Committed)
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            group.RollBack();
                        throw new InvalidOperationException($"族类别修改事务组合并失败：{status}。");
                    }
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started)
                        group.RollBack();
                    throw;
                }
            }

            return result;
        }

        private static Dictionary<long, IList<ElementId>> BuildInstanceIndex(Document document, IList<ElementId> familyIds)
        {
            var selected = new HashSet<long>(familyIds.Select(id => id.GetIdValue()));
            var result = selected.ToDictionary(id => id, _ => (IList<ElementId>)new List<ElementId>());

            foreach (FamilyInstance instance in new FilteredElementCollector(document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>())
            {
                Family family = instance.Symbol?.Family;
                if (family == null) continue;

                long familyId = family.Id.GetIdValue();
                if (selected.Contains(familyId))
                    result[familyId].Add(instance.Id);
            }

            return result;
        }

        private static void ChangeOne(
            Document projectDocument,
            ElementId familyId,
            IList<ElementId> instanceIds,
            FamilyCategoryChangeRequest request,
            FamilyCategoryChangeResult result)
        {
            Family family = projectDocument.GetElement(familyId) as Family;
            if (family == null)
            {
                result.FailedCount++;
                result.Messages.Add($"ID {familyId.GetIdValue()}：族已不存在。");
                return;
            }

            string familyName = family.Name;
            string oldCategory = family.FamilyCategory?.Name ?? "未分类";
            if (!family.IsEditable)
            {
                result.SkippedCount++;
                result.Messages.Add($"{familyName}：不可编辑，已跳过。");
                return;
            }

            if (family.IsInPlace)
            {
                result.SkippedCount++;
                result.Messages.Add($"{familyName}：内建族不支持 EditFamily，已跳过。");
                return;
            }

            if (family.FamilyCategory != null &&
                BuiltInCategoryHelper.TryFromCategory(family.FamilyCategory, out BuiltInCategory current) &&
                current == request.TargetCategory)
            {
                result.SkippedCount++;
                result.Messages.Add($"{familyName}：已经属于“{request.TargetCategoryName}”。");
                return;
            }

            FamilySafetySnapshot safety = CaptureSafetySnapshot(projectDocument, family, instanceIds);
            Document familyDocument = null;
            bool reloadedIntoProject = false;

            try
            {
                familyDocument = projectDocument.EditFamily(family);
                if (familyDocument == null || !familyDocument.IsFamilyDocument)
                    throw new InvalidOperationException("无法打开族文档。");

                Category target = familyDocument.Settings.Categories.get_Item(request.TargetCategory);
                if (target == null)
                    throw new InvalidOperationException($"该族模板不支持目标类别“{request.TargetCategoryName}”。");

                using (var transaction = new Transaction(familyDocument, "修改族类别"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动族文档事务。");

                    try
                    {
                        familyDocument.OwnerFamily.FamilyCategory = target;
                        TransactionStatus status = transaction.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                                transaction.RollBack();
                            throw new InvalidOperationException($"族文档事务提交失败：{status}。");
                        }
                    }
                    catch
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                            transaction.RollBack();
                        throw;
                    }
                }

                // EditFamily 得到的是独立族文档；修改完成后重新载入源项目。
                // 项目侧只保持 TransactionGroup，不启动普通 Transaction，避免 LoadFamily 因 Document.IsModifiable 失败。
                Family reloaded = familyDocument.LoadFamily(projectDocument, new FamilyOption());
                if (reloaded == null)
                    throw new InvalidOperationException("修改后的族未能重新载入项目。");

                reloadedIntoProject = true;
                string actualCategory = reloaded.FamilyCategory?.Name ?? "未分类";
                if (!BuiltInCategoryHelper.TryFromCategory(reloaded.FamilyCategory, out BuiltInCategory actual) ||
                    actual != request.TargetCategory)
                {
                    throw new PostReloadValidationException(
                        $"回载后类别校验失败。期望“{request.TargetCategoryName}”，实际“{actualCategory}”。");
                }

                VerifySafetySnapshot(projectDocument, reloaded, safety);

                result.ChangedCount++;
                result.Messages.Add($"{familyName}：{oldCategory} → {actualCategory}");
            }
            catch (PostReloadValidationException)
            {
                // 已经回载项目后才发现类别/ID/参数值异常：不能把异常状态留在项目中。
                // 向外抛出，让整个 TransactionGroup 回滚，确保此前成功项也恢复。
                throw;
            }
            catch (Exception ex)
            {
                if (reloadedIntoProject)
                {
                    throw new PostReloadValidationException(
                        $"{familyName} 回载项目后发生异常，已触发整批回滚：{ex.Message}");
                }

                result.FailedCount++;
                result.Messages.Add($"{familyName}：失败 - {ex.Message}");
            }
            finally
            {
                if (familyDocument != null && familyDocument.IsValidObject)
                {
                    try { familyDocument.Close(false); }
                    catch (Exception closeEx)
                    {
                        DiagnosticLog.WriteError($"关闭族文档：{familyName}", closeEx);
                    }
                }
            }
        }

        private static FamilySafetySnapshot CaptureSafetySnapshot(Document document, Family family, IList<ElementId> instanceIds)
        {
            var snapshot = new FamilySafetySnapshot { FamilyId = family.Id };

            foreach (ElementId symbolId in family.GetFamilySymbolIds())
            {
                snapshot.SymbolIds.Add(symbolId.GetIdValue());
                Element symbol = document.GetElement(symbolId);
                if (symbol != null)
                    snapshot.Elements.Add(CaptureElementValues(symbol, $"族类型 {symbol.Name}"));
            }

            foreach (ElementId instanceId in instanceIds ?? new List<ElementId>())
            {
                Element instance = document.GetElement(instanceId);
                if (instance != null)
                    snapshot.Elements.Add(CaptureElementValues(instance, $"实例 {instanceId.GetIdValue()}"));
            }

            return snapshot;
        }

        private static ElementSafetySnapshot CaptureElementValues(Element element, string label)
        {
            var snapshot = new ElementSafetySnapshot
            {
                ElementId = element.Id,
                ElementLabel = label
            };

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter == null || !parameter.HasValue) continue;
                long parameterId = parameter.Id.GetIdValue();

                // 只保护正 ID 的项目参数/共享参数/可持久化自定义参数。
                // Revit 内置参数使用负 ID，并会随族类别合法地增删或改变语义，不能作为跨类别一致性门禁。
                if (parameterId <= 0) continue;

                ParameterValueSnapshot value = CaptureParameterValue(parameter);
                if (value != null)
                    snapshot.CustomValues[parameterId] = value;
            }

            return snapshot;
        }

        private static ParameterValueSnapshot CaptureParameterValue(Parameter parameter)
        {
            object value;
            switch (parameter.StorageType)
            {
                case StorageType.Double:
                    value = parameter.AsDouble();
                    break;
                case StorageType.Integer:
                    value = parameter.AsInteger();
                    break;
                case StorageType.String:
                    value = parameter.AsString() ?? string.Empty;
                    break;
                case StorageType.ElementId:
                    value = parameter.AsElementId()?.GetIdValue() ?? ElementId.InvalidElementId.GetIdValue();
                    break;
                default:
                    return null;
            }

            return new ParameterValueSnapshot
            {
                StorageType = parameter.StorageType,
                Value = value
            };
        }

        private static void VerifySafetySnapshot(Document document, Family reloaded, FamilySafetySnapshot snapshot)
        {
            if (reloaded.Id.GetIdValue() != snapshot.FamilyId.GetIdValue())
            {
                throw new PostReloadValidationException(
                    $"族 ElementId 发生变化：{snapshot.FamilyId.GetIdValue()} → {reloaded.Id.GetIdValue()}。");
            }

            var currentSymbolIds = new HashSet<long>(reloaded.GetFamilySymbolIds().Select(id => id.GetIdValue()));
            if (!snapshot.SymbolIds.SetEquals(currentSymbolIds))
            {
                throw new PostReloadValidationException("族类型 ElementId 集合发生变化，已阻止提交以避免引用或数据静默变化。");
            }

            foreach (ElementSafetySnapshot elementSnapshot in snapshot.Elements)
            {
                Element element = document.GetElement(elementSnapshot.ElementId);
                if (element == null)
                {
                    throw new PostReloadValidationException(
                        $"{elementSnapshot.ElementLabel} 在回载后不存在，已阻止提交。");
                }

                foreach (KeyValuePair<long, ParameterValueSnapshot> item in elementSnapshot.CustomValues)
                {
                    Parameter current = FindParameterById(element, item.Key);
                    if (current == null || !current.HasValue)
                    {
                        throw new PostReloadValidationException(
                            $"{elementSnapshot.ElementLabel} 的自定义参数 ID {item.Key} 在类别修改后丢失或无值。");
                    }

                    ParameterValueSnapshot actual = CaptureParameterValue(current);
                    if (!AreEqual(item.Value, actual))
                    {
                        throw new PostReloadValidationException(
                            $"{elementSnapshot.ElementLabel} 的自定义参数 ID {item.Key} 值发生变化。");
                    }
                }
            }
        }

        private static Parameter FindParameterById(Element element, long parameterId)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter != null && parameter.Id.GetIdValue() == parameterId)
                    return parameter;
            }

            return null;
        }

        private static bool AreEqual(ParameterValueSnapshot expected, ParameterValueSnapshot actual)
        {
            if (expected == null || actual == null || expected.StorageType != actual.StorageType)
                return false;

            switch (expected.StorageType)
            {
                case StorageType.Double:
                    return Math.Abs((double)expected.Value - (double)actual.Value) <= 1e-9;
                case StorageType.Integer:
                    return (int)expected.Value == (int)actual.Value;
                case StorageType.String:
                    return string.Equals((string)expected.Value, (string)actual.Value, StringComparison.Ordinal);
                case StorageType.ElementId:
                    return (long)expected.Value == (long)actual.Value;
                default:
                    return false;
            }
        }
    }
}
