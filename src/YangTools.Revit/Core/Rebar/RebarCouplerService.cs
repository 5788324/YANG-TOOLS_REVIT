using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// R10 原生 Rebar Coupler 最小闭环。
    /// 支持 TwoBar 与 SingleEndCap；不以“复制钢筋 + 参数”冒充 Coupler。
    /// dryRun 只做稳定身份/端点/类型/已占用状态预检，不启动 Transaction；
    /// 几何接触、布局和直径兼容性最终以 RebarCoupler.Create 的 RebarCouplerError 为准。
    /// </summary>
    public static class RebarCouplerService
    {
        public static IReadOnlyList<RebarCouplerTypeInfo> GetAvailableTypes(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            ElementId defaultId = GetDefaultCouplerTypeId(doc);
            long defaultValue = defaultId == ElementId.InvalidElementId ? long.MinValue : defaultId.GetIdValue();
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Coupler)
                .Cast<FamilySymbol>()
                .OrderBy(x => x.Family?.Name ?? string.Empty)
                .ThenBy(x => x.Name)
                .Take(500)
                .Select(x => new RebarCouplerTypeInfo
                {
                    ElementId = x.Id.GetIdValue(),
                    FamilyName = x.Family?.Name ?? string.Empty,
                    TypeName = x.Name ?? string.Empty,
                    IsDefault = x.Id.GetIdValue() == defaultValue,
                    MainBarSizeMm = ReadLengthParameterMm(x, BuiltInParameter.COUPLER_MAIN_BAR_SIZE),
                    CoupledBarSizeMm = ReadLengthParameterMm(x, BuiltInParameter.COUPLER_COUPLED_BAR_SIZE),
                    MainEngagementMm = ReadLengthParameterMm(x, BuiltInParameter.COUPLER_MAIN_ENGAGEMENT),
                    CoupledEngagementMm = ReadLengthParameterMm(x, BuiltInParameter.COUPLER_COUPLED_ENGAGEMENT),
                    MainEndTreatmentTypeId = ReadElementIdParameter(x, BuiltInParameter.COUPLER_MAIN_ENDTREATMENT),
                    CoupledEndTreatmentTypeId = ReadElementIdParameter(x, BuiltInParameter.COUPLER_COUPLED_ENDTREATMENT)
                })
                .ToList();
        }

        public static RebarCouplerResult Execute(UIApplication uiapp, RebarCouplerRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure(request, "当前没有活动 Revit 文档。");
            Document doc = uidoc.Document;
            string operationId = string.IsNullOrWhiteSpace(request.OperationId) ? Guid.NewGuid().ToString("N") : request.OperationId.Trim();

            RebarCouplerResult result = Validate(uiapp, doc, request, operationId);
            if (!result.Success)
            {
                DiagnosticLog.WriteProductOperation("Rebar.R10.Coupler", operationId, "REJECTED", doc, result.Error);
                return result;
            }
            if (request.DryRun)
            {
                DiagnosticLog.WriteProductOperation(
                    "Rebar.R10.Coupler", operationId, "DRYRUN_VALIDATED", doc,
                    $"Mode={result.Mode}; TypeId={result.CouplerTypeId}; First={result.FirstRebarId}:{result.FirstEnd}; Second={result.SecondRebarId?.ToString() ?? "<cap>"}:{result.SecondEnd?.ToString() ?? "-"}; ChangedModel=false");
                return result;
            }

            if (doc.GetElement(ElementIdExtensions.CreateId(request.FirstRebarId)) is not Autodesk.Revit.DB.Structure.Rebar first)
                return WithError(result, $"应用前重新解析 First Rebar {request.FirstRebarId} 失败。");
            Autodesk.Revit.DB.Structure.Rebar? second = null;
            if (request.SecondRebarId.HasValue && request.SecondRebarId.Value > 0)
            {
                second = doc.GetElement(ElementIdExtensions.CreateId(request.SecondRebarId.Value)) as Autodesk.Revit.DB.Structure.Rebar;
                if (second == null) return WithError(result, $"应用前重新解析 Second Rebar {request.SecondRebarId.Value} 失败。");
            }
            if (doc.GetElement(ElementIdExtensions.CreateId(result.CouplerTypeId)) is not FamilySymbol couplerType)
                return WithError(result, $"应用前重新解析 Coupler Type {result.CouplerTypeId} 失败。");

            string beforeFirstUid = first.UniqueId ?? string.Empty;
            long beforeFirstHost = first.GetHostId().GetIdValue();
            string beforeSecondUid = second?.UniqueId ?? string.Empty;
            long? beforeSecondHost = second == null ? null : second.GetHostId().GetIdValue();

            Transaction? transaction = null;
            RebarReinforcementData? firstData = null;
            RebarReinforcementData? secondData = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R10 创建钢筋 Coupler");
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动 Revit 事务。");

                firstData = RebarReinforcementData.Create(first.Id, request.FirstEnd);
                if (firstData == null) throw new InvalidOperationException("RebarReinforcementData.Create(first) 返回 null。");
                if (second != null)
                {
                    secondData = RebarReinforcementData.Create(second.Id, request.SecondEnd);
                    if (secondData == null) throw new InvalidOperationException("RebarReinforcementData.Create(second) 返回 null。");
                }

                RebarCouplerError nativeError;
                RebarCoupler? coupler = RebarCoupler.Create(doc, couplerType.Id, firstData, secondData, out nativeError);
                result.NativeValidationPerformed = true;
                result.NativeError = nativeError.ToString();
                if (nativeError != RebarCouplerError.ValidationSuccessfuly || coupler == null)
                    throw new InvalidOperationException("Revit 原生 RebarCoupler.Create 拒绝创建：" + nativeError);

                doc.Regenerate();
                VerifyCreatedCoupler(coupler, first, request.FirstEnd, second, request.SecondEnd, couplerType, result);

                if (!string.Equals(first.UniqueId ?? string.Empty, beforeFirstUid, StringComparison.Ordinal) || first.GetHostId().GetIdValue() != beforeFirstHost)
                    throw new InvalidOperationException("First Rebar 的 UniqueId/Host 在创建 Coupler 后发生变化，已回滚。");
                if (second != null && (!string.Equals(second.UniqueId ?? string.Empty, beforeSecondUid, StringComparison.Ordinal) || second.GetHostId().GetIdValue() != beforeSecondHost))
                    throw new InvalidOperationException("Second Rebar 的 UniqueId/Host 在创建 Coupler 后发生变化，已回滚。");

                TransactionStatus commit = TransactionHelper.ShowSuccessAndCommit(
                    transaction, "钢筋 Coupler R10", "Rebar Coupler 已创建。", uidoc: null, showDialog: false);
                result.TransactionStatus = commit.ToString();
                if (commit != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 事务未成功提交：" + commit);

                result.ChangedModel = true;
                result.ReadbackAfter = RebarInspectionService.Inspect(uiapp, GetIds(first, second));
                result.FirstCouplerIdAfter = ReadCouplerId(first, request.FirstEnd);
                result.SecondCouplerIdAfter = second == null ? null : ReadCouplerId(second, request.SecondEnd);
                result.Message = second == null
                    ? "R10 原生单端 Coupler/End Cap 创建成功。"
                    : "R10 原生 Two-Bar Coupler 创建成功。";
                DiagnosticLog.WriteProductOperation(
                    "Rebar.R10.Coupler", operationId, "COMMITTED", doc,
                    $"Mode={result.Mode}; CouplerId={result.CouplerId}; Native={result.NativeError}; First={result.FirstRebarId}:{result.FirstEnd}; Second={result.SecondRebarId?.ToString() ?? "<cap>"}:{result.SecondEnd?.ToString() ?? "-"}; Transaction={result.TransactionStatus}");
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                }
                catch { }
                result.Success = false;
                result.ChangedModel = false;
                result.Error = ex.Message;
                result.TransactionStatus = transaction?.GetStatus().ToString() ?? string.Empty;
                DiagnosticLog.WriteProductOperation(
                    "Rebar.R10.Coupler", operationId, "FAILED_ROLLEDBACK", doc,
                    $"Native={result.NativeError}; Error={ex.Message}; First={request.FirstRebarId}:{request.FirstEnd}; Second={request.SecondRebarId?.ToString() ?? "<cap>"}:{request.SecondEnd}");
                return result;
            }
            finally
            {
                firstData?.Dispose();
                secondData?.Dispose();
                transaction?.Dispose();
            }
        }

        private static RebarCouplerResult Validate(UIApplication uiapp, Document doc, RebarCouplerRequest request, string operationId)
        {
            var result = new RebarCouplerResult
            {
                OperationId = operationId,
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                FirstRebarId = request.FirstRebarId,
                FirstEnd = request.FirstEnd,
                SecondRebarId = request.SecondRebarId,
                SecondEnd = request.SecondRebarId.HasValue && request.SecondRebarId.Value > 0 ? request.SecondEnd : (int?)null,
                Mode = request.SecondRebarId.HasValue && request.SecondRebarId.Value > 0 ? "TwoBar" : "SingleEndCap"
            };

            if (request.FirstRebarId <= 0) return WithError(result, "FirstRebarId 必须大于 0。");
            if (request.FirstEnd != 0 && request.FirstEnd != 1) return WithError(result, "FirstEnd 只能是 0 或 1。");
            if (doc.GetElement(ElementIdExtensions.CreateId(request.FirstRebarId)) is not Autodesk.Revit.DB.Structure.Rebar first)
                return WithError(result, $"ElementId={request.FirstRebarId} 不是有效 Rebar。");
            if (!string.IsNullOrWhiteSpace(request.ExpectedFirstUniqueId) && !string.Equals(request.ExpectedFirstUniqueId, first.UniqueId ?? string.Empty, StringComparison.Ordinal))
                return WithError(result, "ExpectedFirstUniqueId 与当前 First Rebar 不一致，拒绝执行。");

            result.FirstUniqueId = first.UniqueId ?? string.Empty;
            result.FirstHostId = first.GetHostId().GetIdValue();
            result.FirstCouplerIdBefore = ReadCouplerId(first, request.FirstEnd);
            if (result.FirstCouplerIdBefore.HasValue) return WithError(result, $"First Rebar end {request.FirstEnd} 已存在 Coupler {result.FirstCouplerIdBefore.Value}。");
            if (HasHook(first, request.FirstEnd)) return WithError(result, $"First Rebar end {request.FirstEnd} 当前存在 Hook；R10 不自动移除 Hook。");

            Autodesk.Revit.DB.Structure.Rebar? second = null;
            if (request.SecondRebarId.HasValue && request.SecondRebarId.Value > 0)
            {
                if (request.SecondRebarId.Value == request.FirstRebarId) return WithError(result, "TwoBar 模式下 First/Second Rebar 不能是同一 Element。 ");
                if (request.SecondEnd != 0 && request.SecondEnd != 1) return WithError(result, "SecondEnd 只能是 0 或 1。");
                second = doc.GetElement(ElementIdExtensions.CreateId(request.SecondRebarId.Value)) as Autodesk.Revit.DB.Structure.Rebar;
                if (second == null) return WithError(result, $"ElementId={request.SecondRebarId.Value} 不是有效 Rebar。");
                if (!string.IsNullOrWhiteSpace(request.ExpectedSecondUniqueId) && !string.Equals(request.ExpectedSecondUniqueId, second.UniqueId ?? string.Empty, StringComparison.Ordinal))
                    return WithError(result, "ExpectedSecondUniqueId 与当前 Second Rebar 不一致，拒绝执行。");
                result.SecondUniqueId = second.UniqueId ?? string.Empty;
                result.SecondHostId = second.GetHostId().GetIdValue();
                result.SecondCouplerIdBefore = ReadCouplerId(second, request.SecondEnd);
                if (result.SecondCouplerIdBefore.HasValue) return WithError(result, $"Second Rebar end {request.SecondEnd} 已存在 Coupler {result.SecondCouplerIdBefore.Value}。");
                if (HasHook(second, request.SecondEnd)) return WithError(result, $"Second Rebar end {request.SecondEnd} 当前存在 Hook；R10 不自动移除 Hook。");
                if (first.NumberOfBarPositions != second.NumberOfBarPositions)
                    return WithError(result, $"两组 Rebar 的 bar-position 数量不同（{first.NumberOfBarPositions} vs {second.NumberOfBarPositions}）；R10 不猜测布局对应关系。");
            }

            FamilySymbol? couplerType = ResolveCouplerType(doc, request.CouplerTypeId);
            if (couplerType == null) return WithError(result, request.CouplerTypeId.HasValue
                ? $"CouplerTypeId={request.CouplerTypeId.Value} 不是当前项目有效的 Rebar Coupler FamilySymbol。"
                : "当前项目没有可用的 Rebar Coupler Type；请先加载/创建 Coupler Family Type。");
            result.CouplerTypeId = couplerType.Id.GetIdValue();
            result.CouplerTypeName = (couplerType.Family?.Name ?? string.Empty) + " : " + (couplerType.Name ?? string.Empty);
            double? mainSizeMm = ReadLengthParameterMm(couplerType, BuiltInParameter.COUPLER_MAIN_BAR_SIZE);
            double? coupledSizeMm = ReadLengthParameterMm(couplerType, BuiltInParameter.COUPLER_COUPLED_BAR_SIZE);
            double? firstDiameterMm = ReadBarDiameterMm(doc, first);
            double? secondDiameterMm = second == null ? null : ReadBarDiameterMm(doc, second);
            const double diameterToleranceMm = 0.2;
            if (mainSizeMm.HasValue && mainSizeMm.Value > 0 && firstDiameterMm.HasValue && Math.Abs(mainSizeMm.Value - firstDiameterMm.Value) > diameterToleranceMm)
                return WithError(result, $"Coupler Bar Size 1={mainSizeMm.Value:0.###} mm，与 First Rebar 直径 {firstDiameterMm.Value:0.###} mm 不匹配。");
            if (second != null && coupledSizeMm.HasValue && coupledSizeMm.Value > 0 && secondDiameterMm.HasValue && Math.Abs(coupledSizeMm.Value - secondDiameterMm.Value) > diameterToleranceMm)
                return WithError(result, $"Coupler Bar Size 2={coupledSizeMm.Value:0.###} mm，与 Second Rebar 直径 {secondDiameterMm.Value:0.###} mm 不匹配。");
            if (!mainSizeMm.HasValue || !firstDiameterMm.HasValue)
                result.Warnings.Add("无法静态读出 Coupler Bar Size 1 或 First Rebar 直径；该兼容性留给 RebarCoupler.Create 原生校验。 ");
            if (second != null && (!coupledSizeMm.HasValue || !secondDiameterMm.HasValue))
                result.Warnings.Add("无法静态读出 Coupler Bar Size 2 或 Second Rebar 直径；该兼容性留给 RebarCoupler.Create 原生校验。 ");
            result.ReadbackBefore = RebarInspectionService.Inspect(uiapp, GetIds(first, second));
            result.NativeValidationPerformed = false;
            result.Success = true;
            result.Message = request.DryRun
                ? "R10 dryRun 预检通过；不会启动 Transaction。原生几何/直径兼容性将在 apply 时由 RebarCoupler.Create 验证。"
                : "R10 Coupler 稳定身份与端点预检通过；准备调用 Revit 原生 RebarCoupler.Create。";
            result.Warnings.Add("dryRun 不创建临时 Coupler，因此不冒充 Revit 原生几何/直径兼容性 PASS；apply 的 RebarCouplerError 才是最终 native validation。 ");
            result.Warnings.Add("R10 仅支持普通 Rebar；Area/Path/System Reinforcement/Fabric 不在本阶段范围。 ");
            return result;
        }

        private static void VerifyCreatedCoupler(
            RebarCoupler coupler,
            Autodesk.Revit.DB.Structure.Rebar first,
            int firstEnd,
            Autodesk.Revit.DB.Structure.Rebar? second,
            int secondEnd,
            FamilySymbol requestedType,
            RebarCouplerResult result)
        {
            result.CouplerId = coupler.Id.GetIdValue();
            result.CouplerLinksTwoBars = coupler.CouplerLinkTwoBars();
            result.CouplerQuantity = coupler.GetCouplerQuantity();
            result.PlacementPointCount = coupler.GetPointsForPlacement()?.Count ?? 0;
            if (coupler.GetTypeId().GetIdValue() != requestedType.Id.GetIdValue())
                throw new InvalidOperationException("创建后的 Coupler Type 与请求不一致，已回滚。");
            if ((second != null) != result.CouplerLinksTwoBars.Value)
                throw new InvalidOperationException("CouplerLinkTwoBars 读回与请求模式不一致，已回滚。");

            long? firstCoupler = ReadCouplerId(first, firstEnd);
            if (!firstCoupler.HasValue || firstCoupler.Value != coupler.Id.GetIdValue())
                throw new InvalidOperationException("First Rebar.GetCouplerId(end) 未读回新建 Coupler，已回滚。");
            if (second != null)
            {
                long? secondCoupler = ReadCouplerId(second, secondEnd);
                if (!secondCoupler.HasValue || secondCoupler.Value != coupler.Id.GetIdValue())
                    throw new InvalidOperationException("Second Rebar.GetCouplerId(end) 未读回新建 Coupler，已回滚。");
            }

            IList<ReinforcementData> links = coupler.GetCoupledReinforcementData();
            foreach (ReinforcementData data in links)
            {
                try
                {
                    if (data is RebarReinforcementData rebarData)
                    {
                        result.CoupledRebars.Add(new RebarCouplerLinkReadback
                        {
                            RebarId = rebarData.RebarId.GetIdValue(),
                            End = rebarData.End
                        });
                    }
                }
                finally { data?.Dispose(); }
            }
            if (!result.CoupledRebars.Any(x => x.RebarId == first.Id.GetIdValue() && x.End == firstEnd))
                throw new InvalidOperationException("Coupler reinforcement data 未精确读回 First Rebar/end，已回滚。");
            if (second != null && !result.CoupledRebars.Any(x => x.RebarId == second.Id.GetIdValue() && x.End == secondEnd))
                throw new InvalidOperationException("Coupler reinforcement data 未精确读回 Second Rebar/end，已回滚。");
            if (second == null && result.CoupledRebars.Any(x => x.RebarId != first.Id.GetIdValue()))
                throw new InvalidOperationException("SingleEndCap 读回了意外的第二根 Rebar，已回滚。");
        }

        private static double? ReadLengthParameterMm(Element element, BuiltInParameter parameterId)
        {
            try
            {
                Parameter? parameter = element.get_Parameter(parameterId);
                if (parameter == null || parameter.StorageType != StorageType.Double) return null;
                double internalValue = parameter.AsDouble();
                if (internalValue <= 0) return null;
                return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), 3);
            }
            catch { return null; }
        }

        private static long? ReadElementIdParameter(Element element, BuiltInParameter parameterId)
        {
            try
            {
                Parameter? parameter = element.get_Parameter(parameterId);
                if (parameter == null || parameter.StorageType != StorageType.ElementId) return null;
                ElementId id = parameter.AsElementId();
                return id == null || id == ElementId.InvalidElementId || id.GetIdValue() <= 0 ? (long?)null : id.GetIdValue();
            }
            catch { return null; }
        }

        private static double? ReadBarDiameterMm(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar)
        {
            Element? barType = doc.GetElement(rebar.GetTypeId());
            if (barType == null) return null;
            foreach (string member in new[] { "BarNominalDiameter", "BarDiameter", "NominalDiameter", "BarModelDiameter" })
            {
                try
                {
                    var property = barType.GetType().GetProperty(member);
                    if (property?.GetValue(barType) is double value && value > 0)
                        return Math.Round(UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters), 3);
                }
                catch { }
            }
            return null;
        }

        private static FamilySymbol? ResolveCouplerType(Document doc, long? requestedId)
        {
            if (requestedId.HasValue && requestedId.Value > 0)
            {
                FamilySymbol? requested = doc.GetElement(ElementIdExtensions.CreateId(requestedId.Value)) as FamilySymbol;
                if (requested == null || requested.Category == null || requested.Category.Id.GetIdValue() != (long)BuiltInCategory.OST_Coupler)
                    return null;
                return requested;
            }

            ElementId defaultId = GetDefaultCouplerTypeId(doc);
            if (defaultId != ElementId.InvalidElementId && doc.GetElement(defaultId) is FamilySymbol defaultSymbol)
                return defaultSymbol;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_Coupler)
                .Cast<FamilySymbol>().OrderBy(x => x.Name).FirstOrDefault();
        }

        private static ElementId GetDefaultCouplerTypeId(Document doc)
        {
            try { return doc.GetDefaultFamilyTypeId(ElementIdExtensions.CreateId((long)BuiltInCategory.OST_Coupler)); }
            catch { return ElementId.InvalidElementId; }
        }

        private static bool HasHook(Autodesk.Revit.DB.Structure.Rebar rebar, int end)
        {
            try
            {
                ElementId id = rebar.GetHookTypeId(end);
                return id != null && id != ElementId.InvalidElementId && id.GetIdValue() > 0;
            }
            catch { return false; }
        }

        private static long? ReadCouplerId(Autodesk.Revit.DB.Structure.Rebar rebar, int end)
        {
            try
            {
                ElementId id = rebar.GetCouplerId(end);
                return id == null || id == ElementId.InvalidElementId || id.GetIdValue() <= 0 ? (long?)null : id.GetIdValue();
            }
            catch { return null; }
        }

        private static IList<ElementId> GetIds(Autodesk.Revit.DB.Structure.Rebar first, Autodesk.Revit.DB.Structure.Rebar? second)
        {
            var ids = new List<ElementId> { first.Id };
            if (second != null) ids.Add(second.Id);
            return ids;
        }

        private static RebarCouplerResult Failure(RebarCouplerRequest request, string error) =>
            new RebarCouplerResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                OperationId = string.IsNullOrWhiteSpace(request.OperationId) ? Guid.NewGuid().ToString("N") : request.OperationId,
                FirstRebarId = request.FirstRebarId,
                FirstEnd = request.FirstEnd,
                SecondRebarId = request.SecondRebarId,
                SecondEnd = request.SecondRebarId.HasValue ? request.SecondEnd : (int?)null,
                Error = error
            };

        private static RebarCouplerResult WithError(RebarCouplerResult result, string error)
        {
            result.Success = false;
            result.Error = error;
            return result;
        }
    }
}
