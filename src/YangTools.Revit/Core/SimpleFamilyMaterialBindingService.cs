using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    internal sealed class SimpleFamilyMaterialBindingRequest
    {
        public IList<ElementId> InstanceIds { get; set; } = new List<ElementId>();
        public string ParameterName { get; set; } = "材质";
        public bool IsInstanceParameter { get; set; } = true;
        public bool RebindExistingAssociations { get; set; } = true;
        public ElementId MaterialId { get; set; } = ElementId.InvalidElementId;
        public string MaterialName { get; set; } = string.Empty;
    }

    internal sealed class SimpleFamilyMaterialBindingResult
    {
        public FamilyMaterialBindingResult BindingResult { get; set; } = new FamilyMaterialBindingResult();
        public int RequestedInstanceCount { get; set; }
        public int TargetCount { get; set; }
        public int AssignedCount { get; set; }
        public int AssignmentFailedCount { get; set; }
        public IList<string> Messages { get; } = new List<string>();

        public string BuildSummary(bool instanceMode, string materialName)
        {
            string targetLabel = instanceMode ? "实例" : "族类型";
            string text =
                $"材质：{materialName}\n" +
                $"参数方式：{(instanceMode ? "实例参数" : "类型参数")}\n\n" +
                $"所选实例：{RequestedInstanceCount}\n" +
                $"目标{targetLabel}：{TargetCount}\n" +
                $"成功赋予：{AssignedCount}\n" +
                $"赋值失败：{AssignmentFailedCount}\n\n" +
                $"族绑定：成功 {BindingResult.ChangedCount}，跳过 {BindingResult.SkippedCount}，失败 {BindingResult.FailedCount}\n" +
                $"几何新绑定 {BindingResult.BoundCount}，已绑定 {BindingResult.AlreadyBoundCount}，重绑 {BindingResult.ReboundCount}，几何跳过 {BindingResult.GeometrySkippedCount}";

            var details = new List<string>();
            details.AddRange(Messages);
            details.AddRange(BindingResult.Messages);
            if (details.Count > 0)
            {
                text += "\n\n明细：\n" + string.Join("\n", details.Take(14));
                if (details.Count > 14)
                    text += $"\n…另有 {details.Count - 14} 条";
            }
            return text;
        }
    }

    internal static class SimpleFamilyMaterialBindingService
    {
        internal static SimpleFamilyMaterialBindingResult Execute(
            Document document,
            SimpleFamilyMaterialBindingRequest request)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.IsFamilyDocument) throw new InvalidOperationException("简易绑定材质只能在 Revit 项目文档中运行。");
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.ParameterName = (request.ParameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(request.ParameterName))
                throw new InvalidOperationException("材质参数名称不能为空。");

            Material material = document.GetElement(request.MaterialId) as Material;
            if (material == null)
                throw new InvalidOperationException("选择的项目材质已不存在，请重新选择。");
            request.MaterialName = material.Name;

            List<FamilyInstance> selectedInstances = (request.InstanceIds ?? new List<ElementId>())
                .Select(id => document.GetElement(id) as FamilyInstance)
                .Where(instance => instance != null)
                .GroupBy(instance => instance.Id.GetIdValue())
                .Select(group => group.First())
                .ToList();
            if (selectedInstances.Count == 0)
                throw new InvalidOperationException("没有有效的族实例可处理。");

            List<ElementId> familyIds = selectedInstances
                .Select(instance => instance.Symbol?.Family?.Id)
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
            if (familyIds.Count == 0)
                throw new InvalidOperationException("无法从所选实例解析载入族。");

            var result = new SimpleFamilyMaterialBindingResult
            {
                RequestedInstanceCount = selectedInstances.Count
            };

            using (var group = new TransactionGroup(document, "绑定材质并赋值"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动简易绑定材质事务组。");

                try
                {
                    var bindingRequest = new FamilyMaterialBindingRequest
                    {
                        FamilyIds = familyIds,
                        ParameterName = request.ParameterName,
                        IsInstanceParameter = request.IsInstanceParameter,
                        RebindExistingAssociations = request.RebindExistingAssociations
                    };
                    result.BindingResult = FamilyMaterialBindingService.ExecuteWithinTransactionGroup(document, bindingRequest);

                    AssignMaterialValues(document, request, selectedInstances, result);
                    if (result.AssignedCount == 0)
                    {
                        throw new InvalidOperationException(
                            "没有任何实例或族类型成功取得目标材质。为避免只留下空的材质参数绑定，本次操作已回滚。");
                    }

                    TransactionStatus groupStatus = group.Assimilate();
                    if (groupStatus != TransactionStatus.Committed)
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            group.RollBack();
                        throw new InvalidOperationException($"简易绑定材质事务组合并失败：{groupStatus}。");
                    }
                    return result;
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started)
                    {
                        try { group.RollBack(); } catch { }
                    }
                    throw;
                }
            }
        }

        private static void AssignMaterialValues(
            Document document,
            SimpleFamilyMaterialBindingRequest request,
            IList<FamilyInstance> originallySelected,
            SimpleFamilyMaterialBindingResult result)
        {
            IList<ElementId> targetIds;
            if (request.IsInstanceParameter)
            {
                targetIds = originallySelected.Select(instance => instance.Id).ToList();
            }
            else
            {
                targetIds = originallySelected
                    .Select(instance => document.GetElement(instance.Id) as FamilyInstance)
                    .Where(instance => instance != null && instance.Symbol != null)
                    .Select(instance => instance.Symbol.Id)
                    .GroupBy(id => id.GetIdValue())
                    .Select(group => group.First())
                    .ToList();
            }

            result.TargetCount = targetIds.Count;
            using (var transaction = new Transaction(document, "赋予族材质"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动材质赋值事务。");

                try
                {
                    foreach (ElementId targetId in targetIds)
                    {
                        using (var sub = new SubTransaction(document))
                        {
                            try
                            {
                                if (sub.Start() != TransactionStatus.Started)
                                    throw new InvalidOperationException("无法启动子事务。");

                                Element target = document.GetElement(targetId);
                                if (target == null)
                                    throw new InvalidOperationException("目标实例/类型已不存在。");

                                Parameter parameter = FindWritableMaterialParameter(target, request.ParameterName);
                                if (parameter == null)
                                    throw new InvalidOperationException($"找不到可写的材质参数“{request.ParameterName}”。");

                                if (!parameter.Set(request.MaterialId))
                                    throw new InvalidOperationException("Revit 拒绝写入目标材质。");

                                ElementId actual = parameter.AsElementId();
                                if (actual == null || actual.GetIdValue() != request.MaterialId.GetIdValue())
                                    throw new InvalidOperationException("写入后材质参数校验失败。");

                                TransactionStatus status = sub.Commit();
                                if (status != TransactionStatus.Committed)
                                    throw new InvalidOperationException($"子事务提交失败：{status}。");
                                result.AssignedCount++;
                            }
                            catch (Exception ex)
                            {
                                if (sub.GetStatus() == TransactionStatus.Started)
                                {
                                    try { sub.RollBack(); } catch { }
                                }
                                result.AssignmentFailedCount++;
                                result.Messages.Add($"ID {targetId.GetIdValue()}：{ex.Message}");
                            }
                        }
                    }

                    TransactionStatus transactionStatus = transaction.Commit();
                    if (transactionStatus != TransactionStatus.Committed)
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                            transaction.RollBack();
                        throw new InvalidOperationException($"材质赋值事务提交失败：{transactionStatus}。");
                    }
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try { transaction.RollBack(); } catch { }
                    }
                    throw;
                }
            }
        }

        private static Parameter FindWritableMaterialParameter(Element element, string parameterName)
        {
            IList<Parameter> parameters;
            try { parameters = element.GetParameters(parameterName); }
            catch { return null; }

            foreach (Parameter parameter in parameters ?? new List<Parameter>())
            {
                if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                    continue;
                if (IsMaterialParameter(parameter))
                    return parameter;
            }
            return null;
        }

        private static bool IsMaterialParameter(Parameter parameter)
        {
            try
            {
#if REVIT2022_OR_GREATER
                return parameter.Definition != null && parameter.Definition.GetDataType() == SpecTypeId.Reference.Material;
#else
                return parameter.Definition != null && parameter.Definition.ParameterType == ParameterType.Material;
#endif
            }
            catch
            {
                return false;
            }
        }
    }
}
