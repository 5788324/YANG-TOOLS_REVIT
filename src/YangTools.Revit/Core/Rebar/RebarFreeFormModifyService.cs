using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// R08 unconstrained Free Form 原位曲线修改。
    /// 仅处理由 raw curves 驱动、没有有效 server/constraints 的 Free Form Rebar；
    /// constrained/server-driven Free Form 明确拒绝，留给后续 server/Aligned/Surface 路线。
    /// </summary>
    public static class RebarFreeFormModifyService
    {
        private const double MinSegmentLengthMm = 1.0;
        private const int MaxBars = 500;
        private const int MaxPointsPerBar = 200;

        public static FreeFormRebarModifyResult Execute(UIApplication uiapp, FreeFormRebarModifyRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure(request, "当前没有活动 Revit 文档。");
            Document doc = uidoc.Document;

            FreeFormRebarModifyResult result = Validate(uiapp, doc, request);
            if (!result.Success || request.DryRun) return result;

            if (doc.GetElement(ElementIdExtensions.CreateId(request.RebarId)) is not Autodesk.Revit.DB.Structure.Rebar rebar)
                return Failure(request, $"修改前重新解析 Rebar {request.RebarId} 失败。");

            string beforeUniqueId = rebar.UniqueId ?? string.Empty;
            ElementId beforeHostId = rebar.GetHostId();
            int beforeCount = rebar.NumberOfBarPositions;
            IList<IList<Curve>> curves = BuildCurves(request.Bars);

            Transaction? transaction = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R08 修改 Free Form 曲线");
                if (transaction.Start() != TransactionStatus.Started)
                    return Failure(request, "无法启动 Revit 事务。");

                RebarFreeFormValidationResult validation;
                using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                {
                    if (!accessor.IsUnconstrained())
                        throw new InvalidOperationException("R08 仅允许修改 unconstrained Free Form Rebar。当前钢筋已有约束。");
                    if (accessor.HasValidServer())
                        throw new InvalidOperationException("R08 不修改 server-driven Free Form Rebar。请使用后续 constrained/server 路线。");
                    validation = accessor.SetCurves(curves);
                }

                result.FreeFormValidationResult = validation.ToString();
                if (validation != RebarFreeFormValidationResult.Success)
                    throw new InvalidOperationException("RebarFreeFormAccessor.SetCurves 失败：" + validation);

                doc.Regenerate();

                if (!rebar.IsRebarFreeForm())
                    throw new InvalidOperationException("修改后钢筋不再是 Free Form，已回滚。");
                if (!string.Equals(rebar.UniqueId ?? string.Empty, beforeUniqueId, StringComparison.Ordinal))
                    throw new InvalidOperationException("修改后 UniqueId 发生变化，已回滚。");
                if (rebar.GetHostId() != beforeHostId)
                    throw new InvalidOperationException("修改后 Host 发生变化，已回滚。");
                if (rebar.NumberOfBarPositions != request.Bars.Count)
                    throw new InvalidOperationException($"修改后 bar count={rebar.NumberOfBarPositions}，与请求 {request.Bars.Count} 不一致，已回滚。");

                using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                {
                    if (!accessor.IsUnconstrained() || accessor.HasValidServer())
                        throw new InvalidOperationException("修改后 Free Form 约束/server 状态发生变化，已回滚。");
                }

                TransactionStatus commit = TransactionHelper.ShowSuccessAndCommit(
                    transaction, "Free Form 钢筋修改", "Free Form 钢筋曲线已原位修改。", uidoc: null, showDialog: false);
                result.TransactionStatus = commit.ToString();
                if (commit != TransactionStatus.Committed)
                {
                    result.Success = false;
                    result.Error = "Revit 事务未成功提交：" + commit;
                    return result;
                }

                result.ChangedModel = true;
                result.AfterBarCount = rebar.NumberOfBarPositions;
                result.Message = beforeCount == result.AfterBarCount
                    ? "R08 unconstrained Free Form 曲线原位修改成功。"
                    : $"R08 unconstrained Free Form set 原位修改成功：{beforeCount} → {result.AfterBarCount} bars。";
                result.ReadbackAfter = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
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
                return result;
            }
            finally { transaction?.Dispose(); }
        }

        private static FreeFormRebarModifyResult Validate(UIApplication uiapp, Document doc, FreeFormRebarModifyRequest request)
        {
            var result = new FreeFormRebarModifyResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                RebarId = request.RebarId,
                RequestedBarCount = request.Bars?.Count ?? 0
            };

            if (request.RebarId <= 0) return WithError(result, "RebarId 必须大于 0。");
            if (doc.GetElement(ElementIdExtensions.CreateId(request.RebarId)) is not Autodesk.Revit.DB.Structure.Rebar rebar)
                return WithError(result, $"ElementId={request.RebarId} 不是有效 Rebar。");
            if (!rebar.IsRebarFreeForm()) return WithError(result, "R08 仅支持 Free Form Rebar；Shape Driven 请使用 R04/R05。 ");
            if (!string.IsNullOrWhiteSpace(request.ExpectedUniqueId) &&
                !string.Equals(request.ExpectedUniqueId, rebar.UniqueId ?? string.Empty, StringComparison.Ordinal))
                return WithError(result, "ExpectedUniqueId 与当前 Rebar 不一致，拒绝修改。 ");

            result.UniqueId = rebar.UniqueId ?? string.Empty;
            result.HostId = rebar.GetHostId().GetIdValue();
            result.BeforeBarCount = rebar.NumberOfBarPositions;
            result.AfterBarCount = result.BeforeBarCount;
            result.ReadbackBefore = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });

            try
            {
                using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                {
                    if (!accessor.IsUnconstrained()) return WithError(result, "R08 仅允许修改 unconstrained Free Form Rebar。 ");
                    if (accessor.HasValidServer()) return WithError(result, "R08 不允许修改 server-driven Free Form Rebar。 ");
                }
            }
            catch (Exception ex)
            {
                return WithError(result, "读取 Free Form 状态失败：" + ex.Message);
            }

            if (request.Bars == null || request.Bars.Count == 0) return WithError(result, "Bars 至少需要 1 根钢筋路径。 ");
            if (request.Bars.Count > MaxBars) return WithError(result, $"Bars 不得超过 {MaxBars} 根。 ");

            int segments = 0;
            for (int i = 0; i < request.Bars.Count; i++)
            {
                List<RebarPointMm>? points = request.Bars[i]?.PointsMm;
                if (points == null || points.Count < 2) return WithError(result, $"Bars[{i}] 至少需要 2 个点。 ");
                if (points.Count > MaxPointsPerBar) return WithError(result, $"Bars[{i}] 点数不得超过 {MaxPointsPerBar}。 ");
                for (int p = 1; p < points.Count; p++)
                {
                    XYZ a = ToInternalPoint(points[p - 1]);
                    XYZ b = ToInternalPoint(points[p]);
                    double len = UnitUtils.ConvertFromInternalUnits((b - a).GetLength(), UnitTypeId.Millimeters);
                    if (len < MinSegmentLengthMm) return WithError(result, $"Bars[{i}] 第 {p} 段长度必须 >= {MinSegmentLengthMm} mm。 ");
                    segments++;
                }
            }

            result.RequestedSegmentCount = segments;
            result.Success = true;
            result.Message = request.DryRun
                ? "R08 dryRun 校验通过；不会启动 Transaction。"
                : "R08 Free Form 原位修改参数校验通过。";
            result.Warnings.Add("R08 仅使用 RebarFreeFormAccessor.SetCurves 修改 unconstrained/raw-curve Free Form；遇到有效 server 或约束会拒绝执行。 ");
            result.Warnings.Add("修改 bars 数量会改变 Free Form set 的 bar count；它仍不是 Shape Driven NumberWithSpacing。 ");
            return result;
        }

        private static IList<IList<Curve>> BuildCurves(IList<FreeFormBarPathInput> bars)
        {
            var result = new List<IList<Curve>>();
            foreach (FreeFormBarPathInput bar in bars)
            {
                var curves = new List<Curve>();
                for (int i = 1; i < bar.PointsMm.Count; i++)
                    curves.Add(Line.CreateBound(ToInternalPoint(bar.PointsMm[i - 1]), ToInternalPoint(bar.PointsMm[i])));
                result.Add(curves);
            }
            return result;
        }

        private static XYZ ToInternalPoint(RebarPointMm p) => new XYZ(
            UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Millimeters),
            UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Millimeters),
            UnitUtils.ConvertToInternalUnits(p.Z, UnitTypeId.Millimeters));

        private static FreeFormRebarModifyResult Failure(FreeFormRebarModifyRequest request, string error) =>
            new FreeFormRebarModifyResult { Success = false, DryRun = request.DryRun, ChangedModel = false, RebarId = request.RebarId, Error = error };

        private static FreeFormRebarModifyResult WithError(FreeFormRebarModifyResult result, string error)
        { result.Success = false; result.Error = error; return result; }
    }
}
