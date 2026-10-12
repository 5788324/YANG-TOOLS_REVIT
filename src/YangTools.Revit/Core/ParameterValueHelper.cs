using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Reads and writes parameter values in the same display-unit format shown by Revit.
    /// This avoids treating values such as "1000 mm" as 1000 internal feet.
    /// ElementId parameters are intentionally read-only in generic editors because a name
    /// is not a stable cross-document identifier.
    /// </summary>
    public static class ParameterValueHelper
    {
        public static string GetDisplayValue(Parameter parameter)
        {
            if (parameter == null) return string.Empty;

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.AsString() ?? string.Empty;
                    case StorageType.Double:
                        return parameter.AsValueString()
                            ?? parameter.AsDouble().ToString(CultureInfo.InvariantCulture);
                    case StorageType.Integer:
                        return parameter.AsValueString()
                            ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                    case StorageType.ElementId:
                        return parameter.AsValueString()
                            ?? parameter.AsElementId().GetIdValue().ToString(CultureInfo.InvariantCulture);
                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool CanEditAsText(Parameter parameter)
        {
            return parameter != null
                && !parameter.IsReadOnly
                && parameter.StorageType != StorageType.None
                && parameter.StorageType != StorageType.ElementId;
        }

        public static bool TrySetFromDisplayValue(Parameter parameter, string value, out string error)
        {
            error = null;

            if (parameter == null)
            {
                error = "未找到参数。";
                return false;
            }

            if (parameter.IsReadOnly)
            {
                error = "参数为只读。";
                return false;
            }

            value = value ?? string.Empty;

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        if (!parameter.Set(value))
                        {
                            error = "Revit 拒绝写入字符串值。";
                            return false;
                        }
                        return true;

                    case StorageType.Double:
                        // SetValueString parses the document's current display units.
                        if (parameter.SetValueString(value)) return true;
                        error = "无法按当前项目单位解析该数值。";
                        return false;

                    case StorageType.Integer:
                        if (parameter.SetValueString(value)) return true;

                        if (TryParseInteger(value, out int intValue) && parameter.Set(intValue))
                        {
                            return true;
                        }

                        error = "无法解析整数或是/否值。";
                        return false;

                    case StorageType.ElementId:
                        error = "关联类型参数不能通过普通文本安全修改。";
                        return false;

                    default:
                        error = "不支持该参数存储类型。";
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseInteger(string value, out int result)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result)
                || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "true" || normalized == "yes" || normalized == "是" || normalized == "有")
            {
                result = 1;
                return true;
            }

            if (normalized == "false" || normalized == "no" || normalized == "否" || normalized == "无")
            {
                result = 0;
                return true;
            }

            result = 0;
            return false;
        }
    }
}
