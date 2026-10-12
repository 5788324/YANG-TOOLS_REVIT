using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    internal sealed class FamilyMaterialBindingRequest
    {
        public IList<ElementId> FamilyIds { get; set; } = new List<ElementId>();
        public string ParameterName { get; set; } = "材质";
        public bool IsInstanceParameter { get; set; }
        public bool RebindExistingAssociations { get; set; } = true;
    }

    internal sealed class FamilyMaterialBindingResult
    {
        public int RequestedCount { get; set; }
        public int ChangedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public int GeometryCount { get; set; }
        public int BoundCount { get; set; }
        public int AlreadyBoundCount { get; set; }
        public int ReboundCount { get; set; }
        public int GeometrySkippedCount { get; set; }
        public IList<string> Messages { get; } = new List<string>();

        public string BuildSummary()
        {
            string summary =
                $"请求族：{RequestedCount}\n" +
                $"成功：{ChangedCount}\n" +
                $"跳过：{SkippedCount}\n" +
                $"失败：{FailedCount}\n\n" +
                $"检测到可见材质参数的几何元素：{GeometryCount}\n" +
                $"新绑定：{BoundCount}\n" +
                $"已是目标绑定：{AlreadyBoundCount}\n" +
                $"从其它参数重新绑定：{ReboundCount}\n" +
                $"不可绑定/按策略跳过：{GeometrySkippedCount}";

            if (Messages.Count == 0)
                return summary;

            return summary + "\n\n明细：\n" + string.Join("\n", Messages.Take(16))
                + (Messages.Count > 16 ? $"\n…另有 {Messages.Count - 16} 条" : string.Empty);
        }
    }

    internal static class FamilyMaterialBindingService
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

        private sealed class GeometryBindingStats
        {
            public int GeometryCount { get; set; }
            public int BoundCount { get; set; }
            public int AlreadyBoundCount { get; set; }
            public int ReboundCount { get; set; }
            public int SkippedCount { get; set; }
        }

        private sealed class PostReloadValidationException : InvalidOperationException
        {
            public PostReloadValidationException(string message) : base(message) { }
        }

        private sealed class PreserveProjectValuesFamilyOption : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(
                Family sharedFamily,
                bool familyInUse,
                out FamilySource source,
                out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return true;
            }
        }

        internal static FamilyMaterialBindingResult Execute(Document projectDocument, FamilyMaterialBindingRequest request)
        {
            ValidateRequest(projectDocument, request, out List<ElementId> ids);
            using (var group = new TransactionGroup(projectDocument, "批量绑定族材质参数"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动绑定材质事务组。");

                try
                {
                    FamilyMaterialBindingResult result = ExecuteWithinTransactionGroup(projectDocument, request, ids);
                    TransactionStatus status = group.Assimilate();
                    if (status != TransactionStatus.Committed)
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            group.RollBack();
                        throw new InvalidOperationException($"绑定材质事务组合并失败：{status}。");
                    }
                    return result;
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started)
                        group.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// 在调用方已经启动的 TransactionGroup 内执行族文档绑定。
        /// 这里不会自行 Assimilate，供“简易绑定材质”把族编辑与项目材质赋值合并为一个撤销项。
        /// </summary>
        internal static FamilyMaterialBindingResult ExecuteWithinTransactionGroup(
            Document projectDocument,
            FamilyMaterialBindingRequest request)
        {
            ValidateRequest(projectDocument, request, out List<ElementId> ids);
            return ExecuteWithinTransactionGroup(projectDocument, request, ids);
        }

        private static FamilyMaterialBindingResult ExecuteWithinTransactionGroup(
            Document projectDocument,
            FamilyMaterialBindingRequest request,
            IList<ElementId> ids)
        {
            Dictionary<long, IList<ElementId>> instanceIndex = BuildInstanceIndex(projectDocument, ids);
            var result = new FamilyMaterialBindingResult { RequestedCount = ids.Count };

            foreach (ElementId familyId in ids)
            {
                instanceIndex.TryGetValue(familyId.GetIdValue(), out IList<ElementId> instanceIds);
                BindOne(projectDocument, familyId, instanceIds ?? new List<ElementId>(), request, result);
            }

            return result;
        }

        private static void ValidateRequest(
            Document projectDocument,
            FamilyMaterialBindingRequest request,
            out List<ElementId> ids)
        {
            if (projectDocument == null) throw new ArgumentNullException(nameof(projectDocument));
            if (projectDocument.IsFamilyDocument) throw new InvalidOperationException("绑定材质只能在 Revit 项目文档中运行。");
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.ParameterName = (request.ParameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(request.ParameterName))
                throw new InvalidOperationException("材质参数名称不能为空。");

            ids = (request.FamilyIds ?? new List<ElementId>())
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
            if (ids.Count == 0)
                throw new InvalidOperationException("没有选择需要绑定材质参数的族。");
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

        private static void BindOne(
            Document projectDocument,
            ElementId familyId,
            IList<ElementId> instanceIds,
            FamilyMaterialBindingRequest request,
            FamilyMaterialBindingResult result)
        {
            Family family = projectDocument.GetElement(familyId) as Family;
            if (family == null)
            {
                result.FailedCount++;
                result.Messages.Add($"ID {familyId.GetIdValue()}：族已不存在。");
                return;
            }

            string familyName = family.Name;
            if (!family.IsEditable)
            {
                result.SkippedCount++;
                result.Messages.Add($"{familyName}：不可编辑，已跳过。");
                return;
            }

            if (family.IsInPlace)
            {
                result.SkippedCount++;
                result.Messages.Add($"{familyName}：就地族不支持 EditFamily，已跳过。");
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

                GeometryBindingStats stats;
                using (var transaction = new Transaction(familyDocument, "绑定材质族参数"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动族文档材质绑定事务。");

                    try
                    {
                        IList<Tuple<Element, Parameter>> geometryParameters = CollectMaterialParameters(familyDocument);
                        if (geometryParameters.Count == 0)
                        {
                            transaction.RollBack();
                            result.SkippedCount++;
                            result.Messages.Add($"{familyName}：未找到具有 Material 参数的可处理几何，已跳过且未创建参数。");
                            return;
                        }

                        FamilyParameter familyParameter = GetOrCreateMaterialParameter(
                            familyDocument,
                            request.ParameterName,
                            request.IsInstanceParameter);

                        stats = BindGeometryParameters(
                            familyDocument.FamilyManager,
                            geometryParameters,
                            familyParameter,
                            request.RebindExistingAssociations);

                        if (stats.BoundCount + stats.AlreadyBoundCount + stats.ReboundCount == 0)
                        {
                            transaction.RollBack();
                            result.SkippedCount++;
                            result.GeometryCount += stats.GeometryCount;
                            result.GeometrySkippedCount += stats.SkippedCount;
                            result.Messages.Add($"{familyName}：{stats.GeometryCount} 个几何材质参数均不可绑定或被策略跳过，族文档未修改。");
                            return;
                        }

                        VerifyFamilyDocumentAssociations(
                            familyDocument.FamilyManager,
                            geometryParameters,
                            familyParameter,
                            request.RebindExistingAssociations);

                        TransactionStatus status = transaction.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                                transaction.RollBack();
                            throw new InvalidOperationException($"族文档材质绑定事务提交失败：{status}。");
                        }
                    }
                    catch
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                            transaction.RollBack();
                        throw;
                    }
                }

                Family reloaded = familyDocument.LoadFamily(projectDocument, new PreserveProjectValuesFamilyOption());
                if (reloaded == null)
                    throw new InvalidOperationException("修改后的族未能重新载入项目。");

                reloadedIntoProject = true;
                VerifySafetySnapshot(projectDocument, reloaded, safety);
                VerifyProjectParameterPresence(projectDocument, reloaded, instanceIds, request);

                result.ChangedCount++;
                result.GeometryCount += stats.GeometryCount;
                result.BoundCount += stats.BoundCount;
                result.AlreadyBoundCount += stats.AlreadyBoundCount;
                result.ReboundCount += stats.ReboundCount;
                result.GeometrySkippedCount += stats.SkippedCount;
                string mode = request.IsInstanceParameter ? "实例" : "类型";
                result.Messages.Add(
                    $"{familyName}：完成，参数“{request.ParameterName}”({mode})；几何 {stats.GeometryCount}，新绑 {stats.BoundCount}，已绑 {stats.AlreadyBoundCount}，重绑 {stats.ReboundCount}，跳过 {stats.SkippedCount}。");
            }
            catch (PostReloadValidationException)
            {
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

        private static IList<Tuple<Element, Parameter>> CollectMaterialParameters(Document familyDocument)
        {
            var result = new List<Tuple<Element, Parameter>>();
            foreach (Element element in new FilteredElementCollector(familyDocument)
                .WhereElementIsNotElementType()
                .ToElements())
            {
                if (element == null || element.Id == ElementId.InvalidElementId) continue;

                Parameter parameter;
                try { parameter = element.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM); }
                catch { continue; }
                if (parameter == null) continue;

                result.Add(Tuple.Create(element, parameter));
            }

            return result;
        }

        private static FamilyParameter GetOrCreateMaterialParameter(
            Document familyDocument,
            string parameterName,
            bool isInstance)
        {
            FamilyManager manager = familyDocument.FamilyManager;
            FamilyParameter existing = manager.get_Parameter(parameterName);
            if (existing != null)
            {
                if (!IsMaterialParameter(existing))
                    throw new InvalidOperationException($"族中已存在同名参数“{parameterName}”，但它不是材质参数。请换一个参数名。");
                if (existing.IsInstance != isInstance)
                {
                    string actual = existing.IsInstance ? "实例参数" : "类型参数";
                    string expected = isInstance ? "实例参数" : "类型参数";
                    throw new InvalidOperationException($"族中已存在材质参数“{parameterName}”，但它是{actual}，本次要求{expected}。为避免破坏公式/标签引用，YangTools 不自动替换参数。");
                }

                return existing;
            }

#if REVIT2022_OR_GREATER
            return manager.AddParameter(
                parameterName,
                GroupTypeId.Materials,
                SpecTypeId.Reference.Material,
                isInstance);
#else
            return manager.AddParameter(
                parameterName,
                BuiltInParameterGroup.PG_MATERIALS,
                ParameterType.Material,
                isInstance);
#endif
        }

        private static bool IsMaterialParameter(FamilyParameter parameter)
        {
            if (parameter == null || parameter.Definition == null) return false;
#if REVIT2022_OR_GREATER
            try
            {
                return parameter.Definition.GetDataType() == SpecTypeId.Reference.Material;
            }
            catch
            {
                return false;
            }
#else
            return parameter.Definition.ParameterType == ParameterType.Material;
#endif
        }

        private static GeometryBindingStats BindGeometryParameters(
            FamilyManager manager,
            IList<Tuple<Element, Parameter>> geometryParameters,
            FamilyParameter target,
            bool rebindExisting)
        {
            var stats = new GeometryBindingStats { GeometryCount = geometryParameters.Count };

            foreach (Tuple<Element, Parameter> item in geometryParameters)
            {
                Parameter elementParameter = item.Item2;
                FamilyParameter associated = TryGetAssociatedFamilyParameter(manager, elementParameter);
                if (associated != null)
                {
                    if (SameFamilyParameter(associated, target))
                    {
                        stats.AlreadyBoundCount++;
                        continue;
                    }

                    if (!rebindExisting)
                    {
                        stats.SkippedCount++;
                        continue;
                    }

                    if (!TryDissociate(manager, elementParameter))
                    {
                        stats.SkippedCount++;
                        continue;
                    }

                    if (!CanAssociate(manager, elementParameter))
                    {
                        stats.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        manager.AssociateElementParameterToFamilyParameter(elementParameter, target);
                        stats.ReboundCount++;
                    }
                    catch
                    {
                        stats.SkippedCount++;
                    }
                    continue;
                }

                if (elementParameter.IsReadOnly || !CanAssociate(manager, elementParameter))
                {
                    stats.SkippedCount++;
                    continue;
                }

                try
                {
                    manager.AssociateElementParameterToFamilyParameter(elementParameter, target);
                    stats.BoundCount++;
                }
                catch
                {
                    stats.SkippedCount++;
                }
            }

            return stats;
        }

        private static void VerifyFamilyDocumentAssociations(
            FamilyManager manager,
            IList<Tuple<Element, Parameter>> geometryParameters,
            FamilyParameter target,
            bool rebindExisting)
        {
            // 某些 Revit API 版本若没有公开关联查询方法，则不能可靠地从 API 反查绑定关系。
            // 绑定动作本身已经由 AssociateElementParameterToFamilyParameter 执行；此时跳过“反查式硬校验”，
            // 避免把“API 缺少查询能力”误判成“绑定失败”。真实编译/实机任务仍会在目标版本检查族参数与几何材质关联。
            if (!HasFamilyManagerMethod("GetAssociatedFamilyParameter"))
                return;

            int unresolved = 0;
            foreach (Tuple<Element, Parameter> item in geometryParameters)
            {
                FamilyParameter associated = TryGetAssociatedFamilyParameter(manager, item.Item2);
                if (associated != null && SameFamilyParameter(associated, target))
                    continue;

                // 用户明确选择“不覆盖已有绑定”时，允许保留原来的其它材质参数绑定。
                if (!rebindExisting && associated != null)
                    continue;

                // 只对仍然具备可关联能力的参数做硬校验；只读/不可关联项属于正常跳过。
                if (!item.Item2.IsReadOnly && CanAssociate(manager, item.Item2))
                    unresolved++;
            }

            if (unresolved > 0)
                throw new InvalidOperationException($"族文档绑定后仍有 {unresolved} 个可关联材质参数没有关联到目标族参数。");
        }


        private static bool HasFamilyManagerMethod(string methodName)
        {
            try
            {
                return typeof(FamilyManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(item =>
                        item.Name == methodName &&
                        item.GetParameters().Length == 1 &&
                        item.GetParameters()[0].ParameterType == typeof(Parameter));
            }
            catch
            {
                return false;
            }
        }

        private static FamilyParameter TryGetAssociatedFamilyParameter(FamilyManager manager, Parameter parameter)
        {
            try
            {
                MethodInfo method = typeof(FamilyManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(item =>
                        item.Name == "GetAssociatedFamilyParameter" &&
                        item.GetParameters().Length == 1 &&
                        item.GetParameters()[0].ParameterType == typeof(Parameter));
                return method?.Invoke(manager, new object[] { parameter }) as FamilyParameter;
            }
            catch
            {
                return null;
            }
        }

        private static bool CanAssociate(FamilyManager manager, Parameter parameter)
        {
            try
            {
                MethodInfo method = typeof(FamilyManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(item =>
                        item.Name == "CanElementParameterBeAssociated" &&
                        item.GetParameters().Length == 1 &&
                        item.GetParameters()[0].ParameterType == typeof(Parameter));
                if (method == null)
                    return !parameter.IsReadOnly;

                object value = method.Invoke(manager, new object[] { parameter });
                return value is bool canAssociate && canAssociate;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryDissociate(FamilyManager manager, Parameter parameter)
        {
            try
            {
                MethodInfo method = typeof(FamilyManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(item =>
                        item.Name == "DissociateElementParameterFromFamilyParameter" &&
                        item.GetParameters().Length == 1 &&
                        item.GetParameters()[0].ParameterType == typeof(Parameter));
                if (method == null)
                    return false;

                method.Invoke(manager, new object[] { parameter });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SameFamilyParameter(FamilyParameter left, FamilyParameter right)
        {
            if (left == null || right == null) return false;
            if (ReferenceEquals(left, right)) return true;

            string leftName = left.Definition?.Name ?? string.Empty;
            string rightName = right.Definition?.Name ?? string.Empty;
            return string.Equals(leftName, rightName, StringComparison.Ordinal) &&
                   left.IsInstance == right.IsInstance;
        }

        private static void VerifyProjectParameterPresence(
            Document document,
            Family reloaded,
            IList<ElementId> originalInstanceIds,
            FamilyMaterialBindingRequest request)
        {
            if (request.IsInstanceParameter)
            {
                // 已有实例时，至少一个实例必须暴露新参数。没有实例的族只能依赖族文档侧绑定校验。
                foreach (ElementId id in originalInstanceIds ?? new List<ElementId>())
                {
                    Element instance = document.GetElement(id);
                    if (instance == null) continue;
                    Parameter parameter = instance.LookupParameter(request.ParameterName);
                    if (parameter == null || parameter.StorageType != StorageType.ElementId)
                    {
                        throw new PostReloadValidationException(
                            $"实例 {id.GetIdValue()} 回载后没有材质实例参数“{request.ParameterName}”。");
                    }
                }
                return;
            }

            foreach (ElementId symbolId in reloaded.GetFamilySymbolIds())
            {
                Element symbol = document.GetElement(symbolId);
                if (symbol == null) continue;
                Parameter parameter = symbol.LookupParameter(request.ParameterName);
                if (parameter == null || parameter.StorageType != StorageType.ElementId)
                {
                    throw new PostReloadValidationException(
                        $"族类型 {symbol.Name} 回载后没有材质类型参数“{request.ParameterName}”。");
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
                throw new PostReloadValidationException($"族 ElementId 发生变化：{snapshot.FamilyId.GetIdValue()} → {reloaded.Id.GetIdValue()}。");

            var currentSymbolIds = new HashSet<long>(reloaded.GetFamilySymbolIds().Select(id => id.GetIdValue()));
            if (!snapshot.SymbolIds.SetEquals(currentSymbolIds))
                throw new PostReloadValidationException("族类型 ElementId 集合发生变化，已阻止提交以避免引用或数据静默变化。");

            foreach (ElementSafetySnapshot elementSnapshot in snapshot.Elements)
            {
                Element element = document.GetElement(elementSnapshot.ElementId);
                if (element == null)
                    throw new PostReloadValidationException($"{elementSnapshot.ElementLabel} 在回载后不存在，已阻止提交。");

                foreach (KeyValuePair<long, ParameterValueSnapshot> item in elementSnapshot.CustomValues)
                {
                    Parameter current = FindParameterById(element, item.Key);
                    if (current == null || !current.HasValue)
                        throw new PostReloadValidationException($"{elementSnapshot.ElementLabel} 的既有自定义参数 ID {item.Key} 在材质绑定后丢失或无值。");

                    ParameterValueSnapshot actual = CaptureParameterValue(current);
                    if (!AreEqual(item.Value, actual))
                        throw new PostReloadValidationException($"{elementSnapshot.ElementLabel} 的既有自定义参数 ID {item.Key} 值发生变化。");
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
