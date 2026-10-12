using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using RevitRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// GR01-P0 只读 API/ExternalService 能力探针。
    /// 这里只证明“API surface / server registration 是否存在”，不把存在性冒充 Native Engine Runtime PASS。
    /// </summary>
    public static class RebarGradientCapabilityService
    {
        public static RebarGradientCapabilityReport Probe(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            var report = new RebarGradientCapabilityReport
            {
                Success = false,
                ChangedModel = false,
                RevitVersion = uiapp.Application.VersionNumber ?? string.Empty,
                RevitBuild = uiapp.Application.VersionBuild ?? string.Empty,
                RevitApiAssemblyVersion = typeof(RevitRebar).Assembly.GetName().Version?.ToString() ?? string.Empty
            };

            try
            {
                Type rebarType = typeof(RevitRebar);
                Type accessorType = typeof(RebarFreeFormAccessor);

                PropertyInfo? alignedGuidProperty = rebarType.GetProperty("FreeFormAlignedServerGuid", BindingFlags.Public | BindingFlags.Static);
                PropertyInfo? surfaceGuidProperty = rebarType.GetProperty("FreeFormSurfaceServerGuid", BindingFlags.Public | BindingFlags.Static);
                report.FreeFormAlignedServerGuidPresent = alignedGuidProperty != null;
                report.FreeFormSurfaceServerGuidPresent = surfaceGuidProperty != null;
                Guid? alignedGuid = ReadGuid(alignedGuidProperty);
                Guid? surfaceGuid = ReadGuid(surfaceGuidProperty);
                report.FreeFormAlignedServerGuid = alignedGuid?.ToString("D") ?? string.Empty;
                report.FreeFormSurfaceServerGuid = surfaceGuid?.ToString("D") ?? string.Empty;

                MethodInfo[] createOverloads = rebarType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => string.Equals(m.Name, "CreateFreeForm", StringComparison.Ordinal))
                    .ToArray();
                report.CreateFreeFormOverloads = createOverloads.Select(FormatMethod).OrderBy(x => x).ToList();
                report.ServerCreateFreeFormOverloadPresent = createOverloads.Any(m =>
                {
                    ParameterInfo[] p = m.GetParameters();
                    return p.Length == 4 && p.Any(x => x.ParameterType == typeof(Guid));
                });
                report.ExplicitCurvesCreateFreeFormOverloadPresent = createOverloads.Any(m =>
                {
                    ParameterInfo[] p = m.GetParameters();
                    return p.Length >= 4 && p.All(x => x.ParameterType != typeof(Guid));
                });

                string[] relevantNames =
                {
                    "HasValidAlignedServer", "HasValidSurfaceServer", "HasValidServer", "IsUnconstrained",
                    "GetCustomDistributionPath", "SetLayoutAsMaximumSpacing", "SetLayoutAsNumberWithSpacing",
                    "SetLayoutAsCustomSpacing", "AlignedFreeFormSetOrientationOptions",
                    "SetComputationDataFor3DPathSet", "GetComputationDataFor3DPathSet"
                };
                MethodInfo[] accessorMethods = accessorType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                report.FreeFormAccessorRelevantMembers = accessorMethods
                    .Where(m => relevantNames.Contains(m.Name, StringComparer.Ordinal))
                    .Select(FormatMethod)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                report.HasValidAlignedServerMethodPresent = HasMethod(accessorMethods, "HasValidAlignedServer");
                report.HasValidSurfaceServerMethodPresent = HasMethod(accessorMethods, "HasValidSurfaceServer");
                report.GetCustomDistributionPathPresent = HasMethod(accessorMethods, "GetCustomDistributionPath");
                report.SetLayoutAsMaximumSpacingPresent = HasMethod(accessorMethods, "SetLayoutAsMaximumSpacing");
                report.SetLayoutAsNumberWithSpacingPresent = HasMethod(accessorMethods, "SetLayoutAsNumberWithSpacing");
                report.SetLayoutAsCustomSpacingPresent = HasMethod(accessorMethods, "SetLayoutAsCustomSpacing");
                report.AlignedOrientationOptionsPresent = HasMethod(accessorMethods, "AlignedFreeFormSetOrientationOptions");
                report.ThreeDPathComputationApiPresent =
                    HasMethod(accessorMethods, "SetComputationDataFor3DPathSet") ||
                    HasMethod(accessorMethods, "GetComputationDataFor3DPathSet");

                ProbeRebarUpdateService(report, alignedGuid, surfaceGuid);
                if (report.SetLayoutAsCustomSpacingPresent)
                    report.Warnings.Add("当前运行版本暴露 SetLayoutAsCustomSpacing；仍需 Runtime POC 证明其是否适用于 GR01 目标 Native Engine。 ");
                else
                    report.Warnings.Add("当前运行版本未发现 SetLayoutAsCustomSpacing；GR01 非均匀 Zone 不得假设可由 Native Custom Spacing 直接实现。 ");
                report.Warnings.Add("FreeFormAligned/Surface GUID 或已注册 server 的存在，只是 API/注册信息，不等于能完成 GR01 首末渐变、Cover、Zone 与 Readback。 ");
                report.Success = true;
                return report;
            }
            catch (Exception ex)
            {
                report.Error = ex.Message;
                return report;
            }
        }

        private static void ProbeRebarUpdateService(RebarGradientCapabilityReport report, Guid? alignedGuid, Guid? surfaceGuid)
        {
            try
            {
                Assembly asm = typeof(RevitRebar).Assembly;
                Type? builtInsType = asm.GetType("Autodesk.Revit.DB.ExternalService.ExternalServices+BuiltInExternalServices");
                Type? registryType = asm.GetType("Autodesk.Revit.DB.ExternalService.ExternalServiceRegistry");
                PropertyInfo? serviceIdProperty = builtInsType?.GetProperty("RebarUpdateService", BindingFlags.Public | BindingFlags.Static);
                object? serviceId = serviceIdProperty?.GetValue(null, null);
                if (registryType == null || serviceId == null) return;

                MethodInfo? getService = registryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetService" && m.GetParameters().Length == 1);
                object? service = getService?.Invoke(null, new[] { serviceId });
                if (service == null) return;
                report.RebarUpdateServicePresent = true;

                MethodInfo? getIds = service.GetType().GetMethod("GetRegisteredServerIds", BindingFlags.Public | BindingFlags.Instance);
                object? rawIds = getIds?.Invoke(service, null);
                var ids = new List<Guid>();
                if (rawIds is IEnumerable enumerable)
                {
                    foreach (object? item in enumerable)
                        if (item is Guid guid) ids.Add(guid);
                }
                report.RebarUpdateRegisteredServerCount = ids.Count;
                report.AlignedServerRegistered = alignedGuid.HasValue && ids.Contains(alignedGuid.Value);
                report.SurfaceServerRegistered = surfaceGuid.HasValue && ids.Contains(surfaceGuid.Value);

                MethodInfo? getServer = service.GetType().GetMethod("GetServer", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Guid) }, null);
                foreach (Guid id in ids)
                {
                    string description = id.ToString("D");
                    try
                    {
                        object? server = getServer?.Invoke(service, new object[] { id });
                        if (server != null)
                        {
                            string typeName = server.GetType().FullName ?? server.GetType().Name;
                            string name = InvokeString(server, "GetName");
                            string vendor = InvokeString(server, "GetVendorId");
                            description += $" | {typeName}";
                            if (!string.IsNullOrWhiteSpace(name)) description += $" | Name={name}";
                            if (!string.IsNullOrWhiteSpace(vendor)) description += $" | Vendor={vendor}";
                        }
                    }
                    catch { }
                    report.RegisteredRebarUpdateServers.Add(description);
                }
            }
            catch (Exception ex)
            {
                report.Warnings.Add("读取 RebarUpdateService 注册信息失败：" + ex.Message);
            }
        }

        private static string InvokeString(object target, string methodName)
        {
            try
            {
                MethodInfo? method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                return method?.Invoke(target, null)?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static Guid? ReadGuid(PropertyInfo? property)
        {
            try
            {
                object? value = property?.GetValue(null, null);
                return value is Guid guid ? guid : null;
            }
            catch { return null; }
        }

        private static bool HasMethod(IEnumerable<MethodInfo> methods, string name) =>
            methods.Any(m => string.Equals(m.Name, name, StringComparison.Ordinal));

        private static string FormatMethod(MethodInfo method)
        {
            string args = string.Join(", ", method.GetParameters().Select(p => (p.ParameterType.FullName ?? p.ParameterType.Name) + " " + p.Name));
            return $"{method.ReturnType.FullName ?? method.ReturnType.Name} {method.Name}({args})";
        }
    }
}
