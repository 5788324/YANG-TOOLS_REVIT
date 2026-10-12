using System;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal static class ParameterApiCompatibility
    {
        public static string GetDataTypeKey(Definition definition)
        {
            if (definition == null) return string.Empty;
#if REVIT2022_OR_GREATER
            try
            {
                ForgeTypeId id = definition.GetDataType();
                return id?.TypeId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
#else
            try
            {
                System.Reflection.PropertyInfo? property = definition.GetType().GetProperty("ParameterType");
                object? value = property?.GetValue(definition, null);
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
#endif
        }

        public static string GetGroupTypeKey(Definition definition)
        {
            if (definition == null) return string.Empty;
#if REVIT2022_OR_GREATER
            try
            {
                ForgeTypeId id = definition.GetGroupTypeId();
                return id?.TypeId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
#else
            try
            {
                return definition.ParameterGroup.ToString();
            }
            catch
            {
                return string.Empty;
            }
#endif
        }

        public static bool IsIdentityDataGroup(Definition definition)
        {
            if (definition == null) return false;
#if REVIT2022_OR_GREATER
            try
            {
                return definition.GetGroupTypeId() == GroupTypeId.IdentityData;
            }
            catch
            {
                return false;
            }
#else
            try
            {
                return definition.ParameterGroup == BuiltInParameterGroup.PG_IDENTITY_DATA;
            }
            catch
            {
                return false;
            }
#endif
        }


        public static bool HasGlobalParameterAssociation(Parameter? parameter)
        {
            if (parameter == null) return false;
            try
            {
                // Revit 2018+ 已提供该 API，Revit 2021 可直接调用。
                // 对不支持关联的参数，Revit 返回 InvalidElementId 或抛出可忽略异常。
                ElementId id = parameter.GetAssociatedGlobalParameter();
                return id != null && id != ElementId.InvalidElementId;
            }
            catch
            {
                return false;
            }
        }

        public static bool ReInsert(
            BindingMap bindingMap,
            Definition definition,
            Binding binding)
        {
            if (bindingMap == null) throw new ArgumentNullException(nameof(bindingMap));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (binding == null) throw new ArgumentNullException(nameof(binding));
#if REVIT2022_OR_GREATER
            return bindingMap.ReInsert(definition, binding, definition.GetGroupTypeId());
#else
            return bindingMap.ReInsert(definition, binding, definition.ParameterGroup);
#endif
        }
    }
}
