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
    /// R07 最小 Free Form 创建器。
    /// 当前只创建 unconstrained Free Form Rebar；不伪装成可后加 Shape Driven constraints 的钢筋。
    /// 多个 Bars 直接由 CreateFreeForm 一次创建为一个 Free Form set，其 layout 由 Revit 管理。
    /// </summary>
    public static class RebarFreeFormCreateService
    {
        private const double MinSegmentLengthMm = 1.0;
        private const int MaxBars = 500;
        private const int MaxPointsPerBar = 200;

        public static FreeFormRebarCreateResult Execute(UIApplication uiapp, FreeFormRebarCreateRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure(request, "当前没有活动 Revit 文档。");
            Document doc = uidoc.Document;

            FreeFormRebarCreateResult result = Validate(doc, request);
            if (!result.Success || request.DryRun) return result;

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
            if (host == null || barType == null) return Failure(request, "创建前重新解析宿主或钢筋类型失败。");

            IList<IList<Curve>> curves = BuildCurves(request.Bars);
            Transaction? transaction = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R07 创建 Free Form 钢筋");
                if (transaction.Start() != TransactionStatus.Started)
                    return Failure(request, "无法启动 Revit 事务。");

                RebarFreeFormValidationResult validation;
                Autodesk.Revit.DB.Structure.Rebar rebar = Autodesk.Revit.DB.Structure.Rebar.CreateFreeForm(
                    doc, barType, host, curves, out validation);
                result.FreeFormValidationResult = validation.ToString();
                if (rebar == null || validation != RebarFreeFormValidationResult.Success)
                    throw new InvalidOperationException("Rebar.CreateFreeForm 失败：" + validation);

                doc.Regenerate();
                TransactionStatus commit = TransactionHelper.ShowSuccessAndCommit(
                    transaction, "Free Form 钢筋创建", "Free Form 钢筋已创建。", uidoc: null, showDialog: false);
                result.TransactionStatus = commit.ToString();
                if (commit != TransactionStatus.Committed)
                {
                    result.Success = false;
                    result.Error = "Revit 事务未成功提交：" + commit;
                    return result;
                }

                result.CreatedRebarId = rebar.Id.GetIdValue();
                result.ChangedModel = true;
                result.Message = request.Bars.Count == 1
                    ? "R07 unconstrained Free Form 单根钢筋创建成功。"
                    : "R07 unconstrained Free Form 钢筋集创建成功。";
                result.Readback = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
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

        private static FreeFormRebarCreateResult Validate(Document doc, FreeFormRebarCreateRequest request)
        {
            var result = new FreeFormRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                RebarStyle = NormalizeStyle(request.RebarStyle),
                BarCount = request.Bars?.Count ?? 0
            };
            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            if (host == null) return WithError(result, $"找不到宿主 ElementId={request.HostId}。");
            if (!RebarHostData.IsValidHost(host)) return WithError(result, $"图元 {request.HostId} 不是有效的 Rebar Host。");
            RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
            if (barType == null) return WithError(result, "当前项目中没有可用的 RebarBarType。");
            result.BarTypeId = barType.Id.GetIdValue();
            result.BarTypeName = barType.Name;

            if (request.Bars == null || request.Bars.Count == 0) return WithError(result, "Bars 至少需要 1 根钢筋路径。");
            if (request.Bars.Count > MaxBars) return WithError(result, $"Bars 不得超过 {MaxBars} 根。");
            if (result.RebarStyle != "Standard" && result.RebarStyle != "StirrupTie")
                return WithError(result, "RebarStyle 仅支持 Standard / StirrupTie。");

            int segments = 0;
            for (int i = 0; i < request.Bars.Count; i++)
            {
                var points = request.Bars[i]?.PointsMm;
                if (points == null || points.Count < 2) return WithError(result, $"Bars[{i}] 至少需要 2 个点。");
                if (points.Count > MaxPointsPerBar) return WithError(result, $"Bars[{i}] 点数不得超过 {MaxPointsPerBar}。");
                for (int p = 1; p < points.Count; p++)
                {
                    XYZ a = ToInternalPoint(points[p - 1]);
                    XYZ b = ToInternalPoint(points[p]);
                    double len = UnitUtils.ConvertFromInternalUnits((b - a).GetLength(), UnitTypeId.Millimeters);
                    if (len < MinSegmentLengthMm) return WithError(result, $"Bars[{i}] 第 {p} 段长度必须 >= {MinSegmentLengthMm} mm。");
                    segments++;
                }
            }
            result.SegmentCount = segments;
            result.Success = true;
            result.Message = request.DryRun ? "R07 dryRun 校验通过；不会启动 Transaction。" : "R07 Free Form 创建参数校验通过。";
            result.Warnings.Add("R07 当前使用 Rebar.CreateFreeForm(curves) 创建 unconstrained Free Form；创建后不能再附加 Shape Driven constraints。复杂可约束 Free Form Server 路线留给后续阶段。");
            if (request.Bars.Count > 1)
                result.Warnings.Add("多个 bar paths 会创建为同一个 Free Form set；此类 set 的原生 layout 不是 Shape Driven NumberWithSpacing，不能按 R04 的方式修改 layout。");
            return result;
        }

        private static IList<IList<Curve>> BuildCurves(IList<FreeFormBarPathInput> bars)
        {
            var result = new List<IList<Curve>>();
            foreach (var bar in bars)
            {
                var curves = new List<Curve>();
                for (int i = 1; i < bar.PointsMm.Count; i++)
                    curves.Add(Line.CreateBound(ToInternalPoint(bar.PointsMm[i - 1]), ToInternalPoint(bar.PointsMm[i])));
                result.Add(curves);
            }
            return result;
        }

        private static RebarBarType? ResolveBarType(Document doc, long? requestedId)
        {
            if (requestedId.HasValue && doc.GetElement(ElementIdExtensions.CreateId(requestedId.Value)) is RebarBarType requested)
                return requested;
            return RebarShapeDrivenCreateService.GetAvailableBarTypes(doc).FirstOrDefault();
        }

        private static XYZ ToInternalPoint(RebarPointMm p) => new XYZ(
            UnitUtils.ConvertToInternalUnits(p.X, UnitTypeId.Millimeters),
            UnitUtils.ConvertToInternalUnits(p.Y, UnitTypeId.Millimeters),
            UnitUtils.ConvertToInternalUnits(p.Z, UnitTypeId.Millimeters));

        private static string NormalizeStyle(string? value)
        {
            string s = (value ?? "Standard").Trim();
            if (s.Equals("StirrupTie", StringComparison.OrdinalIgnoreCase) || s.Equals("Stirrup/Tie", StringComparison.OrdinalIgnoreCase)) return "StirrupTie";
            if (s.Equals("Standard", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(s)) return "Standard";
            return s;
        }

        private static FreeFormRebarCreateResult Failure(FreeFormRebarCreateRequest request, string error) =>
            new FreeFormRebarCreateResult { Success = false, DryRun = request.DryRun, ChangedModel = false, HostId = request.HostId, Error = error };

        private static FreeFormRebarCreateResult WithError(FreeFormRebarCreateResult result, string error)
        { result.Success = false; result.Error = error; return result; }
    }
}
