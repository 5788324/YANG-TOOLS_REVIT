using System;
using System.Globalization;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal static class ParameterValueUtil
    {
        public static bool HasMeaningfulValue(Parameter? parameter)
        {
            if (parameter == null) return false;

            try
            {
                if (!parameter.HasValue) return false;

                switch (parameter.StorageType)
                {
                    case StorageType.None:
                        return false;
                    case StorageType.String:
                        return !string.IsNullOrWhiteSpace(parameter.AsString());
                    case StorageType.ElementId:
                        ElementId id = parameter.AsElementId();
                        return id != null && id != ElementId.InvalidElementId;
                    case StorageType.Integer:
                    case StorageType.Double:
                        // 0、False 都是有效参数值，不能按“空”处理。
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool AreEqual(Parameter? left, Parameter? right)
        {
            if (left == null || right == null || left.StorageType != right.StorageType)
                return false;

            try
            {
                switch (left.StorageType)
                {
                    case StorageType.String:
                        return string.Equals(
                            left.AsString() ?? string.Empty,
                            right.AsString() ?? string.Empty,
                            StringComparison.Ordinal);
                    case StorageType.Integer:
                        return left.AsInteger() == right.AsInteger();
                    case StorageType.Double:
                        return Math.Abs(left.AsDouble() - right.AsDouble()) <= 1e-9;
                    case StorageType.ElementId:
                        ElementId leftId = left.AsElementId();
                        ElementId rightId = right.AsElementId();
                        return leftId != null && rightId != null &&
                               leftId.GetIdValue() == rightId.GetIdValue();
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 直接按 Revit 内部存储类型复制，不经过 AsValueString/SetValueString，
        /// 避免单位、精度和区域格式转换造成数据损失。
        /// </summary>
        public static bool TrySetFrom(Parameter? target, Parameter? source, out string error)
        {
            error = string.Empty;
            if (target == null)
            {
                error = "元素上找不到保留参数。";
                return false;
            }

            if (source == null)
            {
                error = "找不到来源参数。";
                return false;
            }

            try
            {
                if (target.IsReadOnly)
                {
                    error = "保留参数为只读参数。";
                    return false;
                }

                if (target.StorageType != source.StorageType)
                {
                    error = "来源参数和保留参数的 StorageType 不一致。";
                    return false;
                }

                bool result;
                switch (source.StorageType)
                {
                    case StorageType.String:
                        result = target.Set(source.AsString() ?? string.Empty);
                        break;
                    case StorageType.Integer:
                        result = target.Set(source.AsInteger());
                        break;
                    case StorageType.Double:
                        result = target.Set(source.AsDouble());
                        break;
                    case StorageType.ElementId:
                        result = target.Set(source.AsElementId());
                        break;
                    default:
                        error = "不支持的参数 StorageType。";
                        return false;
                }

                if (!result && !AreEqual(target, source))
                {
                    error = "Revit Parameter.Set 返回 False。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string ToReportValue(Parameter? parameter)
        {
            if (parameter == null) return string.Empty;

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.AsString() ?? string.Empty;
                    case StorageType.Integer:
                        return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                    case StorageType.Double:
                        return parameter.AsDouble().ToString("R", CultureInfo.InvariantCulture);
                    case StorageType.ElementId:
                        ElementId id = parameter.AsElementId();
                        return id == null ? string.Empty : id.GetIdValue().ToString(CultureInfo.InvariantCulture);
                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ToDisplayValue(Parameter? parameter)
        {
            if (parameter == null) return string.Empty;

            try
            {
                string? formatted = parameter.AsValueString();
                return string.IsNullOrEmpty(formatted) ? ToReportValue(parameter) : formatted;
            }
            catch
            {
                return ToReportValue(parameter);
            }
        }
    }
}
