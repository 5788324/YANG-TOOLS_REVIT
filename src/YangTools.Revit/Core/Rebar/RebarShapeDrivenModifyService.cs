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
    /// R04: conservative in-place modification for existing Shape Driven rebar.
    ///
    /// Scope is intentionally narrow:
    /// - optional RebarBarType change;
    /// - optional NumberWithSpacing layout update;
    /// - preserve ElementId/UniqueId/Host and existing constraint targets;
    /// - preserve varying mode when the set was constraint-driven varying;
    /// - dryRun + before/after R00.3 readback + transaction rollback on invariant loss.
    ///
    /// R04 does not retarget constraints and does not rebuild topology. Those belong to later M2/M3 stages.
    /// </summary>
    public static class RebarShapeDrivenModifyService
    {
        private const int MaxBarPositions = 1002;

        public static ShapeDrivenRebarModifyResult Execute(
            UIApplication uiapp,
            ShapeDrivenRebarModifyRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
                return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            ElementId rebarId = ElementIdExtensions.CreateId(request.RebarId);
            Autodesk.Revit.DB.Structure.Rebar? rebar = doc.GetElement(rebarId) as Autodesk.Revit.DB.Structure.Rebar;
            if (rebar == null)
                return Failure(request, $"找不到 Rebar ElementId={request.RebarId}。");
            if (rebar.IsRebarFreeForm())
                return Failure(request, "R04 只允许修改 Shape Driven Rebar；Free Form 不在本阶段。");

            string originalUniqueId = rebar.UniqueId;
            if (!string.IsNullOrWhiteSpace(request.ExpectedUniqueId) &&
                !string.Equals(request.ExpectedUniqueId, originalUniqueId, StringComparison.Ordinal))
                return Failure(request, "ExpectedUniqueId 与当前 Rebar 不一致；拒绝修改可能已变化的目标。");

            RebarInspectionReport beforeReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
            RebarInspectionItem? before = beforeReport.Items.FirstOrDefault();
            if (before == null || !string.IsNullOrWhiteSpace(before.Error))
                return Failure(request, "无法读取修改前 Rebar 快照。", beforeReport);
            if (!string.Equals(before.EngineMode, "ShapeDriven", StringComparison.OrdinalIgnoreCase))
                return Failure(request, $"R04 仅支持 ShapeDriven，当前为 {before.EngineMode}。", beforeReport);

            RebarBarType? targetBarType = null;
            if (request.BarTypeId.HasValue)
            {
                targetBarType = doc.GetElement(ElementIdExtensions.CreateId(request.BarTypeId.Value)) as RebarBarType;
                if (targetBarType == null)
                    return Failure(request, $"BarTypeId={request.BarTypeId.Value} 不是有效 RebarBarType。", beforeReport);
            }

            bool updateLayout = request.NumberOfBarPositions.HasValue || request.SpacingMm.HasValue;
            int targetCount = request.NumberOfBarPositions ?? before.NumberOfBarPositions;
            double targetSpacingMm = request.SpacingMm ?? before.MaxSpacingMm ?? 0.0;
            if (updateLayout)
            {
                if (targetCount < 1 || targetCount > MaxBarPositions)
                    return Failure(request, $"NumberOfBarPositions 必须在 1..{MaxBarPositions} 之间。", beforeReport);
                if (targetSpacingMm <= 0)
                    return Failure(request, "SpacingMm 必须大于 0。", beforeReport);
            }

            long beforeTypeId = rebar.GetTypeId().GetIdValue();
            bool typeChangeRequested = targetBarType != null && targetBarType.Id != rebar.GetTypeId();
            bool layoutChangeRequested = updateLayout &&
                (!string.Equals(before.LayoutRule, "NumberWithSpacing", StringComparison.OrdinalIgnoreCase) ||
                 before.NumberOfBarPositions != targetCount ||
                 !NearlyEqual(before.MaxSpacingMm, targetSpacingMm));

            if (!typeChangeRequested && !layoutChangeRequested)
                return Failure(request, "R04 没有检测到实际修改：BarType / NumberWithSpacing 均与当前值一致。", beforeReport);

            string beforeConstraintSignature = BuildConstraintSignature(before);
            string beforeVaryingMode = RebarApiCompatibility.ReadVaryingMode(rebar);
            bool preserveVarying = IsVarying(beforeVaryingMode, before);

            var result = new ShapeDrivenRebarModifyResult
            {
                Success = true,
                DryRun = request.DryRun,
                ChangedModel = false,
                RebarId = request.RebarId,
                UniqueId = originalUniqueId,
                HostId = before.HostId,
                BeforeBarTypeId = beforeTypeId,
                AfterBarTypeId = beforeTypeId,
                BarTypeChanged = typeChangeRequested,
                BeforeLayoutRule = before.LayoutRule,
                AfterLayoutRule = before.LayoutRule,
                BeforeNumberOfBarPositions = before.NumberOfBarPositions,
                AfterNumberOfBarPositions = before.NumberOfBarPositions,
                BeforeSpacingMm = before.MaxSpacingMm,
                AfterSpacingMm = before.MaxSpacingMm,
                LayoutChanged = layoutChangeRequested,
                BeforeVaryingMode = beforeVaryingMode,
                AfterVaryingMode = beforeVaryingMode,
                BeforeConstraintSignature = beforeConstraintSignature,
                AfterConstraintSignature = beforeConstraintSignature,
                SameElementId = true,
                SameUniqueId = true,
                SameHost = true,
                ConstraintsPreserved = true,
                BeforeInspection = beforeReport,
                AfterInspection = beforeReport,
                Message = request.DryRun
                    ? "R04 dryRun：目标、类型/布局变更和约束保护条件已解析；未启动 Transaction。"
                    : "R04 Shape Driven 修改参数校验通过。"
            };

            if (request.DryRun)
            {
                if (typeChangeRequested) result.AfterBarTypeId = targetBarType!.Id.GetIdValue();
                if (layoutChangeRequested)
                {
                    result.AfterLayoutRule = "NumberWithSpacing";
                    result.AfterNumberOfBarPositions = targetCount;
                    result.AfterSpacingMm = targetSpacingMm;
                }
                return result;
            }

            Transaction? transaction = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R04 Shape Driven 原位修改");
                TransactionStatus startStatus = transaction.Start();
                if (startStatus != TransactionStatus.Started)
                    return WithError(result, $"无法启动 Revit 事务：{startStatus}");

                if (typeChangeRequested)
                {
                    ElementId replacement = rebar.ChangeTypeId(targetBarType!.Id);
                    if (replacement != ElementId.InvalidElementId && replacement != rebar.Id)
                        throw new InvalidOperationException(
                            $"ChangeTypeId 产生了替代 ElementId={replacement.GetIdValue()}；R04 禁止隐式重建，事务回滚。");
                }

                if (layoutChangeRequested)
                {
                    using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
                    {
                        double spacingInternal = UnitUtils.ConvertToInternalUnits(targetSpacingMm, UnitTypeId.Millimeters);
                        accessor.SetLayoutAsNumberWithSpacing(
                            targetCount,
                            spacingInternal,
                            request.BarsOnNormalSide,
                            request.IncludeFirstBar,
                            request.IncludeLastBar);
                    }
                    if (preserveVarying)
                        RebarApiCompatibility.SetShapeDrivenVaryingEnabled(rebar, true);
                }

                doc.Regenerate();

                RebarInspectionReport midReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                RebarInspectionItem? mid = midReport.Items.FirstOrDefault();
                if (mid == null || !string.IsNullOrWhiteSpace(mid.Error))
                    throw new InvalidOperationException("修改后、提交前无法读取 Rebar 快照。");

                bool sameId = mid.ElementId == request.RebarId;
                bool sameUnique = string.Equals(mid.UniqueId, originalUniqueId, StringComparison.Ordinal);
                bool sameHost = mid.HostId == before.HostId &&
                    string.Equals(mid.HostUniqueId, before.HostUniqueId, StringComparison.Ordinal);
                string afterConstraintSignature = BuildConstraintSignature(mid);
                bool constraintsPreserved = string.Equals(
                    beforeConstraintSignature,
                    afterConstraintSignature,
                    StringComparison.Ordinal);

                if (!sameId || !sameUnique || !sameHost)
                    throw new InvalidOperationException("R04 修改破坏了 ElementId / UniqueId / Host 不变量；事务回滚。");
                if (request.RequireConstraintPreservation && !constraintsPreserved)
                    throw new InvalidOperationException("R04 修改改变了既有约束类型或目标；事务回滚。");

                TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                    transaction,
                    "钢筋修改",
                    "R04 Shape Driven 钢筋已原位修改。",
                    uidoc: null,
                    showDialog: false);
                result.TransactionStatus = commitStatus.ToString();
                if (commitStatus != TransactionStatus.Committed)
                {
                    result.Success = false;
                    result.Error = $"Revit 事务未成功提交：{commitStatus}";
                    return result;
                }

                RebarInspectionReport afterReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                RebarInspectionItem? after = afterReport.Items.FirstOrDefault();
                if (after == null)
                {
                    result.Success = false;
                    result.Error = "事务已提交，但无法读取修改后的 Rebar。";
                    return result;
                }

                result.ChangedModel = true;
                result.AfterInspection = afterReport;
                result.AfterBarTypeId = rebar.GetTypeId().GetIdValue();
                result.AfterLayoutRule = after.LayoutRule;
                result.AfterNumberOfBarPositions = after.NumberOfBarPositions;
                result.AfterSpacingMm = after.MaxSpacingMm;
                result.AfterVaryingMode = RebarApiCompatibility.ReadVaryingMode(rebar);
                result.AfterConstraintSignature = BuildConstraintSignature(after);
                result.SameElementId = after.ElementId == request.RebarId;
                result.SameUniqueId = string.Equals(after.UniqueId, originalUniqueId, StringComparison.Ordinal);
                result.SameHost = after.HostId == before.HostId &&
                    string.Equals(after.HostUniqueId, before.HostUniqueId, StringComparison.Ordinal);
                result.ConstraintsPreserved = string.Equals(
                    beforeConstraintSignature,
                    result.AfterConstraintSignature,
                    StringComparison.Ordinal);
                result.Message = "R04 Shape Driven Rebar 已原位修改；ElementId/Host/Constraint 已完成 readback。";
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
                result.AfterInspection = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                return result;
            }
            finally
            {
                try { transaction?.Dispose(); } catch { }
            }
        }

        private static bool IsVarying(string mode, RebarInspectionItem item)
        {
            if (item.UseConstraintsForVaryingBars == true) return true;
            if (string.IsNullOrWhiteSpace(mode)) return false;
            return mode.IndexOf("Vary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mode.IndexOf("Constraint", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildConstraintSignature(RebarInspectionItem item)
        {
            return string.Join(" || ", item.Constraints
                .Select(c => $"{c.HandleType}|{c.ConstraintType}|{c.TargetSummary}")
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static bool NearlyEqual(double? current, double target)
        {
            return current.HasValue && Math.Abs(current.Value - target) <= 0.001;
        }

        private static ShapeDrivenRebarModifyResult Failure(
            ShapeDrivenRebarModifyRequest request,
            string error,
            RebarInspectionReport? before = null)
        {
            return new ShapeDrivenRebarModifyResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                RebarId = request.RebarId,
                Error = error,
                BeforeInspection = before,
                AfterInspection = before
            };
        }

        private static ShapeDrivenRebarModifyResult WithError(
            ShapeDrivenRebarModifyResult result,
            string error)
        {
            result.Success = false;
            result.ChangedModel = false;
            result.Error = error;
            return result;
        }
    }
}
