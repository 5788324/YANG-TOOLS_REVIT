using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

using RevitRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// GR01-P0 server-driven Free Form 最小“骨架创建”探针。
    /// 所有尝试都强制 RollBack，仅用于 disposable test document；成功也不等于 Surface/Aligned 几何能力 PASS。
    /// </summary>
    public static class RebarGradientServerCreateProbeService
    {
        public static RebarGradientServerCreateProbeResult Execute(UIApplication uiapp, RebarGradientServerCreateProbeRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure(request, "当前没有活动 Revit 文档。 ");
            Document doc = uidoc.Document;

            var result = new RebarGradientServerCreateProbeResult
            {
                Success = false,
                ChangedModel = false,
                Mode = NormalizeMode(request.Mode),
                HostId = request.HostId,
                BarTypeId = request.BarTypeId
            };

            if (request.HostId <= 0) return WithError(result, "HostId 必须大于 0。 ");
            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            if (host == null) return WithError(result, $"找不到 Host ElementId={request.HostId}。 ");
            if (!RebarHostData.IsValidHost(host)) return WithError(result, "目标不是有效 Rebar Host。 ");

            RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
            if (barType == null) return WithError(result, "当前项目中没有可用 RebarBarType。 ");
            result.BarTypeId = barType.Id.GetIdValue();

            PropertyInfo? guidProperty = typeof(RevitRebar).GetProperty(
                result.Mode == "Aligned" ? "FreeFormAlignedServerGuid" : "FreeFormSurfaceServerGuid",
                BindingFlags.Public | BindingFlags.Static);
            if (guidProperty?.GetValue(null, null) is not Guid serverGuid || serverGuid == Guid.Empty)
                return WithError(result, $"当前 Revit API 未提供 {result.Mode} Free Form server GUID。 ");
            result.ServerGuid = serverGuid.ToString("D");

            MethodInfo? createMethod = typeof(RevitRebar).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "CreateFreeForm", StringComparison.Ordinal)) return false;
                    ParameterInfo[] p = m.GetParameters();
                    return p.Length == 4 && p[0].ParameterType == typeof(Document) && p[1].ParameterType == typeof(Guid);
                });
            if (createMethod == null) return WithError(result, "当前 Revit API 未找到 CreateFreeForm(Document, Guid, RebarBarType, Element) overload。 ");

            Transaction? tx = null;
            try
            {
                tx = new Transaction(doc, $"YangTools GR01-P0 {result.Mode} Server Probe");
                if (tx.Start() != TransactionStatus.Started)
                    return WithError(result, "无法启动 POC Transaction。 ");

                object? created = createMethod.Invoke(null, new object[] { doc, serverGuid, barType, host });
                if (created is RevitRebar rebar)
                {
                    result.CreateReturnedRebar = true;
                    result.TemporaryRebarId = rebar.Id.GetIdValue();
                    result.IsFreeForm = rebar.IsRebarFreeForm();
                    result.NumberOfBarPositions = rebar.NumberOfBarPositions;
                    try
                    {
                        using RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor();
                        result.IsUnconstrained = accessor.IsUnconstrained();
                        result.HasValidServer = accessor.HasValidServer();
                        result.AccessorServerGuid = accessor.GetServerGUID().ToString("D");
                        try { result.CustomDistributionPathCurveCount = accessor.GetCustomDistributionPath()?.Count; }
                        catch (Exception ex) { result.Warnings.Add("读取 CustomDistributionPath 失败：" + ex.Message); }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("读取 FreeFormAccessor 状态失败：" + ex.Message);
                    }
                }

                doc.Regenerate();
                result.Success = result.CreateReturnedRebar;
                result.Warnings.Add("本工具只验证 built-in server GUID 是否能创建最小 server-driven Rebar skeleton；没有配置 Surface/Aligned handles/constraints，因此不能据此宣布 Native Surface/Aligned 可用于 GR01。 ");
                return result;
            }
            catch (TargetInvocationException ex)
            {
                result.Error = ex.InnerException?.Message ?? ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                if (tx != null)
                {
                    try
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                        {
                            TransactionStatus rolled = tx.RollBack();
                            result.TransactionStatus = rolled.ToString();
                        }
                        else result.TransactionStatus = tx.GetStatus().ToString();
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add("POC RollBack 状态读取失败：" + ex.Message);
                    }
                    tx.Dispose();
                }
                result.ChangedModel = false;
                result.TemporaryRebarId = null;
            }
        }

        private static RebarBarType? ResolveBarType(Document doc, long? requestedId)
        {
            if (requestedId.HasValue && doc.GetElement(ElementIdExtensions.CreateId(requestedId.Value)) is RebarBarType requested)
                return requested;
            return RebarShapeDrivenCreateService.GetAvailableBarTypes(doc).FirstOrDefault();
        }

        private static string NormalizeMode(string? mode) =>
            string.Equals(mode?.Trim(), "Aligned", StringComparison.OrdinalIgnoreCase) ? "Aligned" : "Surface";

        private static RebarGradientServerCreateProbeResult Failure(RebarGradientServerCreateProbeRequest request, string error) =>
            new RebarGradientServerCreateProbeResult
            {
                Success = false,
                ChangedModel = false,
                Mode = NormalizeMode(request.Mode),
                HostId = request.HostId,
                BarTypeId = request.BarTypeId,
                Error = error
            };

        private static RebarGradientServerCreateProbeResult WithError(RebarGradientServerCreateProbeResult result, string error)
        {
            result.Success = false;
            result.ChangedModel = false;
            result.Error = error;
            return result;
        }
    }
}
