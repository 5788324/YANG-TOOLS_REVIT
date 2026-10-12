using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal sealed class ParameterScanService
    {
        private readonly Document _document;

        public ParameterScanService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public ScanResult Scan(Action<string>? log)
        {
            var result = new ScanResult();
            var timer = Stopwatch.StartNew();

            log?.Invoke("正在读取项目参数绑定……");
            ReadBoundProjectParameters(result);

            log?.Invoke("正在检查明细表和视图过滤器引用……");
            new ReferenceScanService(_document).ApplyReferences(result.AllParameters, result);

            log?.Invoke("正在生成重复参数合并计划……");
            BuildDuplicateGroups(result);

            log?.Invoke(
                $"扫描完成：项目参数 {result.AllParameters.Count} 个，唯一名称 {result.DistinctNameCount} 个，" +
                $"重复名称 {result.Groups.Count} 组，可自动处理 {result.CompatibleGroupCount} 组；" +
                $"耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");

            return result;
        }

        private void ReadBoundProjectParameters(ScanResult result)
        {
            DefinitionBindingMapIterator iterator = _document.ParameterBindings.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext())
            {
                Definition? definition = iterator.Key;
                ElementBinding? binding = iterator.Current as ElementBinding;
                if (definition == null || binding == null) continue;

                if (!(definition is InternalDefinition internalDefinition)) continue;

                ParameterElement? parameterElement;
                try
                {
                    parameterElement = _document.GetElement(internalDefinition.Id) as ParameterElement;
                }
                catch
                {
                    parameterElement = null;
                }

                if (parameterElement == null) continue;

                string name = definition.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var record = new ParameterRecord
                {
                    Name = name,
                    NormalizedName = name.Trim(),
                    Id = parameterElement.Id.GetIdValue(),
                    Definition = definition,
                    Element = parameterElement,
                    Binding = binding,
                    BindingKind = binding is InstanceBinding
                        ? BindingKind.Instance
                        : binding is TypeBinding
                            ? BindingKind.Type
                            : BindingKind.Unknown,
                    DataTypeKey = ParameterApiCompatibility.GetDataTypeKey(definition),
                    GroupTypeKey = ParameterApiCompatibility.GetGroupTypeKey(definition),
                    IsIdentityDataGroup = ParameterApiCompatibility.IsIdentityDataGroup(definition),
                    IsShared = parameterElement is SharedParameterElement
                };

                if (parameterElement is SharedParameterElement shared)
                {
                    try { record.SharedGuid = shared.GuidValue; }
                    catch { record.SharedGuid = null; }
                }

                try
                {
                    foreach (Category category in binding.Categories)
                    {
                        if (category != null)
                            record.CategoryIds.Add(category.Id.GetIdValue());
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "读取参数绑定类别",
                        ParameterName = record.Name,
                        ParameterId = record.Id,
                        Message = ex.Message
                    });
                }

                result.AllParameters.Add(record);
            }
        }

        private static void BuildDuplicateGroups(ScanResult result)
        {
            IEnumerable<IGrouping<string, ParameterRecord>> duplicateNames = result.AllParameters
                .GroupBy(x => x.NormalizedName, StringComparer.Ordinal)
                .Where(x => x.Count() > 1)
                .OrderBy(x => x.Key, StringComparer.CurrentCulture);

            foreach (IGrouping<string, ParameterRecord> nameGroup in duplicateNames)
            {
                var plan = new DuplicateGroupPlan { Name = nameGroup.Key };
                plan.Records.AddRange(nameGroup.OrderBy(x => x.Id));

                BindingKind[] bindingKinds = plan.Records
                    .Select(x => x.BindingKind)
                    .Distinct()
                    .ToArray();
                string[] dataTypes = plan.Records
                    .Select(x => x.DataTypeKey)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                if (bindingKinds.Length != 1 || bindingKinds[0] == BindingKind.Unknown)
                {
                    plan.IsCompatible = false;
                    plan.Selected = false;
                    plan.IncompatibilityReason = "同名参数同时包含实例参数和类型参数，无法合并为一个绑定。";
                    plan.Status = "不兼容：" + plan.IncompatibilityReason;
                    result.Groups.Add(plan);
                    continue;
                }

                if (dataTypes.Length != 1)
                {
                    plan.IsCompatible = false;
                    plan.Selected = false;
                    plan.IncompatibilityReason = "同名参数的数据类型不同，不能安全复制参数值。";
                    plan.Status = "不兼容：" + plan.IncompatibilityReason;
                    result.Groups.Add(plan);
                    continue;
                }

                plan.Keeper = SelectKeeper(plan.Records);
                plan.IsCompatible = true;
                plan.Status = "可处理";
                result.Groups.Add(plan);
            }
        }

        private static ParameterRecord SelectKeeper(IEnumerable<ParameterRecord> records)
        {
            return records
                .OrderByDescending(x => x.IsReferenced)
                .ThenByDescending(x => x.ScheduleReferences.Count + x.FilterReferences.Count)
                .ThenByDescending(x => x.IsShared)
                .ThenByDescending(x => x.IsIdentityDataGroup)
                .ThenByDescending(x => x.CategoryIds.Count)
                .ThenBy(x => x.Id)
                .First();
        }
    }
}
