using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal sealed class ReferenceScanService
    {
        private readonly Document _document;

        public ReferenceScanService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public void ApplyReferences(IList<ParameterRecord> records, ScanResult result)
        {
            var byId = records.ToDictionary(x => x.Id);
            ScanScheduleReferences(byId, result);
            ScanFilterReferences(byId, result);
        }

        private void ScanScheduleReferences(
            IReadOnlyDictionary<long, ParameterRecord> byId,
            ScanResult result)
        {
            foreach (ViewSchedule schedule in new FilteredElementCollector(_document)
                         .OfClass(typeof(ViewSchedule))
                         .Cast<ViewSchedule>())
            {
                try
                {
                    ScheduleDefinition? definition = schedule.Definition;
                    if (definition == null) continue;

                    ScanScheduleDefinition(schedule, definition, "主明细表", byId);

                    // 各版本 EmbeddedDefinition 暴露情况不同，使用反射兼容。
                    PropertyInfo? embeddedProperty = definition.GetType().GetProperty(
                        "EmbeddedDefinition",
                        BindingFlags.Instance | BindingFlags.Public);
                    if (embeddedProperty?.GetValue(definition, null) is ScheduleDefinition embedded)
                        ScanScheduleDefinition(schedule, embedded, "嵌入明细表", byId);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "扫描明细表引用",
                        ElementId = schedule.Id.GetIdValue(),
                        ElementUniqueId = SafeUniqueId(schedule),
                        Message = schedule.Name + "：" + GetInnermostMessage(ex)
                    });
                }
            }
        }

        private static void ScanScheduleDefinition(
            ViewSchedule schedule,
            ScheduleDefinition definition,
            string sectionName,
            IReadOnlyDictionary<long, ParameterRecord> byId)
        {
            foreach (ScheduleFieldId fieldId in definition.GetFieldOrder())
            {
                ScheduleField? field = definition.GetField(fieldId);
                if (field?.ParameterId == null) continue;

                long parameterId = field.ParameterId.GetIdValue();
                if (!byId.TryGetValue(parameterId, out ParameterRecord? record)) continue;

                string reference = $"{schedule.Name} / {sectionName} (Id {schedule.Id.GetIdValue()})";
                if (!record.ScheduleReferences.Contains(reference))
                    record.ScheduleReferences.Add(reference);
            }
        }

        private void ScanFilterReferences(
            IReadOnlyDictionary<long, ParameterRecord> byId,
            ScanResult result)
        {
            foreach (ParameterFilterElement filter in new FilteredElementCollector(_document)
                         .OfClass(typeof(ParameterFilterElement))
                         .Cast<ParameterFilterElement>())
            {
                try
                {
                    var ids = new HashSet<long>();
                    object? root = InvokeNoArgumentMethod(filter, "GetElementFilter");

                    if (root != null)
                    {
                        WalkFilterObject(root, ids, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
                    }
                    else
                    {
                        object? rules = InvokeNoArgumentMethod(filter, "GetRules");
                        if (rules == null)
                        {
                            result.FilterReferenceScanIncomplete = true;
                            result.Errors.Add(new ErrorEntry
                            {
                                Stage = "扫描视图过滤器引用",
                                ElementId = filter.Id.GetIdValue(),
                                ElementUniqueId = SafeUniqueId(filter),
                                Message = filter.Name + "：当前 Revit API 未返回可解析的过滤规则。"
                            });
                        }
                        else
                        {
                            WalkFilterObject(rules, ids, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
                        }
                    }

                    foreach (long id in ids)
                    {
                        if (!byId.TryGetValue(id, out ParameterRecord? record)) continue;
                        string reference = $"{filter.Name} (Id {filter.Id.GetIdValue()})";
                        if (!record.FilterReferences.Contains(reference))
                            record.FilterReferences.Add(reference);
                    }
                }
                catch (Exception ex)
                {
                    result.FilterReferenceScanIncomplete = true;
                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "扫描视图过滤器引用",
                        ElementId = filter.Id.GetIdValue(),
                        ElementUniqueId = SafeUniqueId(filter),
                        Message = filter.Name + "：" + GetInnermostMessage(ex)
                    });
                }
            }
        }

        private static object? InvokeNoArgumentMethod(object? target, string methodName)
        {
            if (target == null) return null;

            MethodInfo? method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            return method?.Invoke(target, null);
        }

        private static void WalkFilterObject(
            object? value,
            ISet<long> parameterIds,
            ISet<object> visited,
            int depth)
        {
            if (value == null || depth > 12 || value is string) return;

            if (value is ElementId elementId)
            {
                parameterIds.Add(elementId.GetIdValue());
                return;
            }

            Type type = value.GetType();
            if (!type.IsValueType)
            {
                if (!visited.Add(value)) return;
            }

            object? ruleParameter = InvokeNoArgumentMethod(value, "GetRuleParameter");
            if (ruleParameter is ElementId ruleParameterId)
                parameterIds.Add(ruleParameterId.GetIdValue());

            string[] nestedMethods = { "GetRules", "GetFilters", "GetInnerRule" };
            foreach (string methodName in nestedMethods)
            {
                object? nested = InvokeNoArgumentMethod(value, methodName);
                if (nested != null)
                    WalkFilterObject(nested, parameterIds, visited, depth + 1);
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                    WalkFilterObject(item, parameterIds, visited, depth + 1);
            }
        }

        private static string SafeUniqueId(Element element)
        {
            try { return element.UniqueId ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string GetInnermostMessage(Exception ex)
        {
            Exception current = ex;
            while (current.InnerException != null) current = current.InnerException;
            return current.Message;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
