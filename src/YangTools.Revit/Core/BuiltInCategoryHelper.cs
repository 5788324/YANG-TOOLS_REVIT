using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    internal static class BuiltInCategoryHelper
    {
        public static bool TryFromCategory(Category category, out BuiltInCategory value)
        {
            value = BuiltInCategory.INVALID;
            if (category == null)
                return false;

#if REVIT2024_OR_GREATER
            long rawValue = category.Id.Value;
#else
            long rawValue = category.Id.IntegerValue;
#endif
            return TryFromInt64(rawValue, out value);
        }

        public static bool TryFromCategoryId(ElementId categoryId, out BuiltInCategory value)
        {
            value = BuiltInCategory.INVALID;
            if (categoryId == null || categoryId == ElementId.InvalidElementId)
                return false;

            return TryFromInt64(categoryId.GetIdValue(), out value);
        }

        public static bool TryFromInt64(long rawValue, out BuiltInCategory value)
        {
            value = BuiltInCategory.INVALID;
            try
            {
                Type enumType = typeof(BuiltInCategory);
                Type underlyingType = Enum.GetUnderlyingType(enumType);
                object enumValue;

                if (underlyingType == typeof(long))
                {
                    enumValue = Enum.ToObject(enumType, rawValue);
                }
                else if (underlyingType == typeof(int))
                {
                    if (rawValue < int.MinValue || rawValue > int.MaxValue)
                        return false;
                    enumValue = Enum.ToObject(enumType, (int)rawValue);
                }
                else
                {
                    object typedValue = Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
                    enumValue = Enum.Parse(enumType, typedValue.ToString() ?? string.Empty);
                }

                if (!Enum.IsDefined(enumType, enumValue))
                    return false;

                value = (BuiltInCategory)enumValue;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static long ToInt64(BuiltInCategory value)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }
}
