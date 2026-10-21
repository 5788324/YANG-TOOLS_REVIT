using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using RevitRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// GR01 V1：Explicit ManagedFreeForm 渐变钢筋集。
    ///
    /// 设计边界：
    /// - 两根 single-position Rebar 只作为首末几何模板，不会被修改或加入结果集；
    /// - DistributionPath 仅负责 Station，V1 接受全局 Z 轴、直线 ModelLine / Edge；
    /// - Zone 由 RebarGradientSpacingPlanner 计算；
    /// - 所有 bar position 由首末 transformed centerline 线性插值；
    /// - 最终一次 Rebar.CreateFreeForm 创建一个 Rebar Element + N physical bars；
    /// - Explicit 路线是 ManagedGeometry，不冒充 Native ToCover；
    /// - V1 只做 Preview/Create，不做 Update。
    /// </summary>
    public static class RebarGradientSetService
    {
        private const double MinCurveLengthMm = 1.0;
        private const double ArcCollinearRatioTolerance = 1e-8;

        private sealed class CurveDescriptor
        {
            public string Kind { get; set; } = string.Empty;
            public XYZ Start { get; set; } = XYZ.Zero;
            public XYZ Mid { get; set; } = XYZ.Zero;
            public XYZ End { get; set; } = XYZ.Zero;
            public double LengthInternal { get; set; }

            public CurveDescriptor Reversed()
            {
                return new CurveDescriptor
                {
                    Kind = Kind,
                    Start = End,
                    Mid = Mid,
                    End = Start,
                    LengthInternal = LengthInternal
                };
            }
        }

        private sealed class PathState
        {
            public string Kind { get; set; } = string.Empty;
            public long? ElementId { get; set; }
            public string UniqueId { get; set; } = string.Empty;
            public string StableReference { get; set; } = string.Empty;
            public XYZ RawStart { get; set; } = XYZ.Zero;
            public XYZ RawEnd { get; set; } = XYZ.Zero;
            public XYZ UnitDirection { get; set; } = XYZ.BasisZ;
            public double RawLengthInternal { get; set; }
        }

        private sealed class BuildState
        {
            public RevitRebar StartRebar { get; set; } = null!;
            public RevitRebar EndRebar { get; set; } = null!;
            public Element Host { get; set; } = null!;
            public RebarBarType BarType { get; set; } = null!;
            public RebarStyle Style { get; set; }
            public PathState Path { get; set; } = null!;
            public List<CurveDescriptor> StartCurves { get; set; } = new List<CurveDescriptor>();
            public List<CurveDescriptor> EndCurves { get; set; } = new List<CurveDescriptor>();
            public List<List<CurveDescriptor>> PlannedBars { get; set; } = new List<List<CurveDescriptor>>();
            public RebarGradientSpacingPlanResult SpacingPlan { get; set; } = null!;
        }

        public static RebarGradientSetResult Execute(UIApplication uiapp, RebarGradientSetRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            string operationId = string.IsNullOrWhiteSpace(request.OperationId)
                ? "GR01-" + Guid.NewGuid().ToString("N")
                : request.OperationId.Trim();

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                return new RebarGradientSetResult
                {
                    Success = false,
                    DryRun = request.DryRun,
                    ChangedModel = false,
                    OperationId = operationId,
                    ErrorCode = "NO_ACTIVE_DOCUMENT",
                    Error = "当前没有活动 Revit 文档。"
                };
            }

            Document doc = uidoc.Document;
            RebarGradientSetResult result = CreateBaseResult(request, operationId);
            BuildState? state = null;
            try
            {
                state = BuildPlan(doc, request, result);
                if (state == null || !result.Success)
                {
                    DiagnosticLog.WriteProductOperation(
                        "Rebar.GR01.GradientSet", operationId, "REJECTED", doc,
                        $"Code={result.ErrorCode}; {result.Error}");
                    return result;
                }

                if (request.DryRun)
                {
                    result.Message = $"GR01 V1 Preview 通过：{result.BarCount} 根，Engine=ExplicitManagedFreeForm；未启动 Transaction。";
                    DiagnosticLog.WriteProductOperation(
                        "Rebar.GR01.GradientSet", operationId, "DRYRUN_VALIDATED", doc,
                        $"Bars={result.BarCount}; Path={result.DistributionLengthMm:0.###}mm; Fingerprint={result.PlanFingerprint}");
                    return result;
                }

                return CreateSet(uiapp, doc, request, state, result);
            }
            catch (GradientValidationException ex)
            {
                result.Success = false;
                result.ChangedModel = false;
                result.ErrorCode = ex.Code;
                result.Error = ex.Message;
                DiagnosticLog.WriteProductOperation(
                    "Rebar.GR01.GradientSet", operationId, "REJECTED", doc,
                    $"Code={result.ErrorCode}; {result.Error}");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ChangedModel = false;
                result.ErrorCode = "UNEXPECTED_ERROR";
                result.Error = ex.Message;
                DiagnosticLog.WriteProductOperation(
                    "Rebar.GR01.GradientSet", operationId, "FAILED", doc, ex.ToString());
                return result;
            }
        }

        private static RebarGradientSetResult CreateSet(
            UIApplication uiapp,
            Document doc,
            RebarGradientSetRequest request,
            BuildState state,
            RebarGradientSetResult result)
        {
#if REVIT2021
            // Revit 2021 has no FreeFormAccessor.RebarStyle getter/setter; creating a
            // style-unverifiable rebar would violate GR01's lossless readback contract.
            // Reject BEFORE Transaction.Start until a version-specific, runtime-verified
            // style-preserving implementation is provided.
            throw new GradientValidationException(
                "FREEFORM_STYLE_API_UNSUPPORTED_REVIT2021",
                "Revit 2021 不支持 GR01 自由形式钢筋样式的可靠设置与回读，已在事务前阻止创建。请使用 Revit 2022+ 验证，或等待 2021 专用实现。");
#endif
            IList<IList<Curve>> curves = BuildRevitCurves(state.PlannedBars);
            Transaction? tx = null;
            RevitRebar? created = null;
            try
            {
                tx = new Transaction(doc, "YangTools GR01 创建渐变钢筋集");
                TransactionStatus started = tx.Start();
                if (started != TransactionStatus.Started)
                    throw new GradientValidationException("TRANSACTION_START_FAILED", "无法启动 Revit 事务：" + started);

                // Create 前再次确认控制筋和路径仍与 Preview/本次 Plan 一致。
                string liveFingerprint = RebuildFingerprintOnly(doc, request);
                if (!string.Equals(liveFingerprint, result.PlanFingerprint, StringComparison.Ordinal))
                    throw new GradientValidationException("PLAN_CHANGED_BEFORE_CREATE", "创建前模型状态已变化；请重新 Preview。 ");

                RebarFreeFormValidationResult validation;
                created = RevitRebar.CreateFreeForm(doc, state.BarType, state.Host, curves, out validation);
                result.FreeFormValidationResult = validation.ToString();
                if (created == null || validation != RebarFreeFormValidationResult.Success)
                    throw new GradientValidationException("FREEFORM_CREATE_FAILED", "Rebar.CreateFreeForm 失败：" + validation);

                // Revit 2025 explicit CreateFreeForm(out validation) 没有 style 参数；用 FreeFormAccessor 写回控制筋 style。
#if !REVIT2021
                using (RebarFreeFormAccessor accessor = created.GetFreeFormAccessor())
                {
                    accessor.RebarStyle = state.Style;
                }
#endif

                doc.Regenerate();

                VerifyCreatedSet(doc, state, created, request.GeometryReadbackToleranceMm, out double maxDeviationMm);
                VerifyControlsUnchanged(state, request.GeometryReadbackToleranceMm);

                result.CreatedRebarId = created.Id.GetIdValue();
                result.CreatedRebarUniqueId = created.UniqueId ?? string.Empty;
                result.PhysicalBarCount = created.NumberOfBarPositions;
                result.MaxReadbackDeviationMm = Math.Round(maxDeviationMm, 6);

                TransactionStatus committed = tx.Commit();
                result.TransactionStatus = committed.ToString();
                if (committed != TransactionStatus.Committed)
                    throw new GradientValidationException("TRANSACTION_COMMIT_FAILED", "Revit 事务未成功提交：" + committed);

                // 从这里开始事务已经 Committed；后续辅助读回失败不得再谎称 ChangedModel=false。
                result.Success = true;
                result.ChangedModel = true;
                result.Message = $"GR01 V1 渐变钢筋集创建成功：1 个 Rebar Element / {result.PhysicalBarCount} physical bars。";
                try
                {
                    result.Readback = RebarInspectionService.Inspect(uiapp, new[] { created.Id });
                }
                catch (Exception inspectionException)
                {
                    result.Warnings.Add("事务已提交，但 R00 辅助 Inspection 失败：" + inspectionException.Message);
                }

                DiagnosticLog.WriteProductOperation(
                    "Rebar.GR01.GradientSet", result.OperationId, "COMMITTED", doc,
                    $"RebarId={result.CreatedRebarId}; Bars={result.PhysicalBarCount}; " +
                    $"Validation={result.FreeFormValidationResult}; MaxDeviation={result.MaxReadbackDeviationMm:0.######}mm; " +
                    $"Fingerprint={result.PlanFingerprint}");
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    if (tx != null && tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                }
                catch { }

                result.Success = false;
                result.ChangedModel = false;
                if (ex is GradientValidationException validationException)
                {
                    result.ErrorCode = validationException.Code;
                    result.Error = validationException.Message;
                }
                else
                {
                    result.ErrorCode = "CREATE_EXCEPTION";
                    result.Error = ex.Message;
                }
                result.TransactionStatus = tx?.GetStatus().ToString() ?? string.Empty;
                result.CreatedRebarId = null;
                result.CreatedRebarUniqueId = string.Empty;
                result.PhysicalBarCount = null;
                result.Readback = null;

                DiagnosticLog.WriteProductOperation(
                    "Rebar.GR01.GradientSet", result.OperationId, "FAILED_ROLLEDBACK", doc,
                    $"Code={result.ErrorCode}; {ex}");
                return result;
            }
            finally
            {
                try { tx?.Dispose(); } catch { }
            }
        }

        private static BuildState? BuildPlan(Document doc, RebarGradientSetRequest request, RebarGradientSetResult result)
        {
            ValidateRequestSettings(request);

            if (request.StartControlRebarId <= 0 || request.EndControlRebarId <= 0)
                throw new GradientValidationException("INVALID_CONTROL_REBAR_ID", "Start/End Control RebarId 必须大于 0。 ");
            if (request.StartControlRebarId == request.EndControlRebarId)
                throw new GradientValidationException("SAME_CONTROL_REBAR", "起始控制筋与结束控制筋不能是同一 Rebar。 ");

            RevitRebar start = doc.GetElement(ElementIdExtensions.CreateId(request.StartControlRebarId)) as RevitRebar
                ?? throw new GradientValidationException("START_CONTROL_NOT_REBAR", $"ElementId={request.StartControlRebarId} 不是有效 Rebar。 ");
            RevitRebar end = doc.GetElement(ElementIdExtensions.CreateId(request.EndControlRebarId)) as RevitRebar
                ?? throw new GradientValidationException("END_CONTROL_NOT_REBAR", $"ElementId={request.EndControlRebarId} 不是有效 Rebar。 ");

            if (!string.IsNullOrWhiteSpace(request.ExpectedStartControlUniqueId) &&
                !string.Equals(request.ExpectedStartControlUniqueId, start.UniqueId, StringComparison.Ordinal))
                throw new GradientValidationException("START_CONTROL_UID_MISMATCH", "起始控制筋 ExpectedUniqueId 与当前模型不一致。 ");
            if (!string.IsNullOrWhiteSpace(request.ExpectedEndControlUniqueId) &&
                !string.Equals(request.ExpectedEndControlUniqueId, end.UniqueId, StringComparison.Ordinal))
                throw new GradientValidationException("END_CONTROL_UID_MISMATCH", "结束控制筋 ExpectedUniqueId 与当前模型不一致。 ");

            if (start.NumberOfBarPositions != 1 || end.NumberOfBarPositions != 1)
                throw new GradientValidationException("CONTROL_NOT_SINGLE_POSITION", "GR01 V1 控制筋必须都是 single-position Rebar。 ");
            if (!DoesBarExist(start, 0) || !DoesBarExist(end, 0))
                throw new GradientValidationException("CONTROL_BAR_POSITION_MISSING", "控制筋的唯一 Bar Position 不存在。 ");

            ElementId startHostId = start.GetHostId();
            ElementId endHostId = end.GetHostId();
            if (startHostId == ElementId.InvalidElementId || endHostId == ElementId.InvalidElementId || startHostId != endHostId)
                throw new GradientValidationException("CONTROL_HOST_MISMATCH", "两根控制筋必须具有同一个有效 Rebar Host。 ");
            Element host = doc.GetElement(startHostId)
                ?? throw new GradientValidationException("HOST_NOT_FOUND", "无法解析控制筋 Host。 ");
            if (!RebarHostData.IsValidHost(host))
                throw new GradientValidationException("INVALID_REBAR_HOST", "控制筋 Host 不是有效 Rebar Host。 ");

            ElementId startTypeId = start.GetTypeId();
            ElementId endTypeId = end.GetTypeId();
            if (startTypeId == ElementId.InvalidElementId || startTypeId != endTypeId)
                throw new GradientValidationException("CONTROL_BAR_TYPE_MISMATCH", "两根控制筋必须使用相同 RebarBarType。 ");
            RebarBarType barType = doc.GetElement(startTypeId) as RebarBarType
                ?? throw new GradientValidationException("BAR_TYPE_NOT_FOUND", "无法解析控制筋 RebarBarType。 ");

            RebarStyle startStyle = ReadRebarStyle(doc, start);
            RebarStyle endStyle = ReadRebarStyle(doc, end);
            if (startStyle != endStyle)
                throw new GradientValidationException("CONTROL_STYLE_MISMATCH", "两根控制筋 RebarStyle 必须一致。 ");

            RejectUnsupportedEndFeatures(start, "起始控制筋");
            RejectUnsupportedEndFeatures(end, "结束控制筋");

            List<CurveDescriptor> startCurves = DescribeCurves(GetActualPositionCenterlineCurves(start, 0));
            List<CurveDescriptor> rawEndCurves = DescribeCurves(GetActualPositionCenterlineCurves(end, 0));
            ValidateCurveSequence(startCurves, request, "起始控制筋");
            ValidateCurveSequence(rawEndCurves, request, "结束控制筋");

            if (startCurves.Count != rawEndCurves.Count)
                throw new GradientValidationException("CONTROL_CURVE_COUNT_MISMATCH", $"控制筋曲线数量不一致：{startCurves.Count} vs {rawEndCurves.Count}。 ");
            if (startCurves.Count > request.MaxCurvesPerBar)
                throw new GradientValidationException("CURVE_LIMIT_EXCEEDED", $"每根控制筋曲线数量不得超过 {request.MaxCurvesPerBar}。 ");

            bool forwardValid = TryMatchScore(startCurves, rawEndCurves, out double forwardScore);
            List<CurveDescriptor> reversedCandidate = ReverseSequence(rawEndCurves);
            bool reverseValid = TryMatchScore(startCurves, reversedCandidate, out double reverseScore);
            if (!forwardValid && !reverseValid)
                throw new GradientValidationException("CONTROL_CURVE_TOPOLOGY_MISMATCH", "两根控制筋的 Line/Arc 拓扑无法按正序或反序一一对应。 ");

            bool endReversed = reverseValid && (!forwardValid || reverseScore + 1e-9 < forwardScore);
            List<CurveDescriptor> endCurves = endReversed ? reversedCandidate : rawEndCurves;

            XYZ startCenter = ComputeGeometryCenter(startCurves);
            XYZ endCenter = ComputeGeometryCenter(endCurves);
            PathState path = ResolveDistributionPath(doc, request.DistributionPath, startCenter, endCenter);
            double startProjection = (startCenter - path.RawStart).DotProduct(path.UnitDirection);
            double endProjection = (endCenter - path.RawStart).DotProduct(path.UnitDirection);
            double projectionTolerance = ToInternal(request.PathProjectionToleranceMm);
            if (Math.Min(startProjection, endProjection) < -projectionTolerance ||
                Math.Max(startProjection, endProjection) > path.RawLengthInternal + projectionTolerance)
            {
                throw new GradientValidationException(
                    "CONTROL_OUTSIDE_PATH_EXTENT",
                    $"控制筋投影超出 DistributionPath 有效范围（容差 {request.PathProjectionToleranceMm:0.###}mm）。 ");
            }

            double distributionLengthInternal = Math.Abs(endProjection - startProjection);
            double distributionLengthMm = ToMillimeters(distributionLengthInternal);
            if (distributionLengthMm < MinCurveLengthMm)
                throw new GradientValidationException("DISTRIBUTION_LENGTH_TOO_SHORT", "两根控制筋沿 DistributionPath 的有效距离必须 >= 1mm。 ");

            List<RebarGradientSpacingZoneInput> normalizedZones = NormalizeZonesForDistribution(
                request.Zones ?? new List<RebarGradientSpacingZoneInput>(),
                distributionLengthMm,
                request.LengthToleranceMm);

            var spacingRequest = new RebarGradientSpacingPlanRequest
            {
                DistributionLengthMm = distributionLengthMm,
                Zones = normalizedZones,
                SpacingStepMm = request.SpacingStepMm,
                MinTailRatio = request.MinTailRatio,
                LengthToleranceMm = request.LengthToleranceMm,
                StationMergeToleranceMm = request.StationMergeToleranceMm,
                MaxZones = request.MaxZones,
                MaxPhysicalBars = request.MaxPhysicalBars
            };
            RebarGradientSpacingPlanResult spacing = RebarGradientSpacingPlanner.Calculate(spacingRequest);
            if (!spacing.Success)
                throw new GradientValidationException(spacing.ErrorCode, spacing.Error);

            var plannedBars = new List<List<CurveDescriptor>>(spacing.BarCount);
            for (int i = 0; i < spacing.StationsMm.Count; i++)
            {
                double t = spacing.DistributionLengthMm <= 0.0 ? 0.0 : spacing.StationsMm[i] / spacing.DistributionLengthMm;
                t = Math.Max(0.0, Math.Min(1.0, t));
                List<CurveDescriptor> bar = InterpolateBar(startCurves, endCurves, t, request);
                plannedBars.Add(bar);
            }

            result.StartControlUniqueId = start.UniqueId ?? string.Empty;
            result.EndControlUniqueId = end.UniqueId ?? string.Empty;
            result.EndControlReversed = endReversed;
            result.ControlCurveCount = startCurves.Count;
            result.HostId = host.Id.GetIdValue();
            result.HostUniqueId = host.UniqueId ?? string.Empty;
            result.BarTypeId = barType.Id.GetIdValue();
            result.BarTypeName = barType.Name;
            result.RebarStyle = startStyle.ToString();
            result.DistributionPathKind = path.Kind;
            result.DistributionPathElementId = path.ElementId;
            result.DistributionPathUniqueId = path.UniqueId;
            result.DistributionPathStableReference = path.StableReference;
            result.PathReversed = endProjection < startProjection;
            result.RawPathLengthMm = ToMillimeters(path.RawLengthInternal);
            result.StartProjectionMm = ToMillimeters(startProjection);
            result.EndProjectionMm = ToMillimeters(endProjection);
            result.DistributionLengthMm = distributionLengthMm;
            result.SpacingPlan = spacing;
            result.BarCount = spacing.BarCount;
            result.IntervalCount = spacing.IntervalCount;
            result.FirstBarSample = BuildBarSample(0, spacing, plannedBars[0]);
            int middleIndex = spacing.BarCount / 2;
            result.MiddleBarSample = BuildBarSample(middleIndex, spacing, plannedBars[middleIndex]);
            result.LastBarSample = BuildBarSample(spacing.BarCount - 1, spacing, plannedBars[spacing.BarCount - 1]);

            var state = new BuildState
            {
                StartRebar = start,
                EndRebar = end,
                Host = host,
                BarType = barType,
                Style = startStyle,
                Path = path,
                StartCurves = startCurves,
                EndCurves = endCurves,
                PlannedBars = plannedBars,
                SpacingPlan = spacing
            };

            result.PlanFingerprint = ComputePlanFingerprint(request, result, state);
            if (!string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint) &&
                !string.Equals(request.ExpectedPlanFingerprint.Trim(), result.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new GradientValidationException("PLAN_FINGERPRINT_MISMATCH", "ExpectedPlanFingerprint 与当前模型/参数计算结果不一致；请重新 Preview。 ");
            }

            result.Success = true;
            result.ChangedModel = false;
            result.Warnings.Add("GR01 V1 使用 Explicit ManagedFreeForm；结果为 unconstrained Free Form，NativeToCover=false。 ");
            result.Warnings.Add("DistributionPath 只决定 Station；钢筋几何和方向由两根控制筋 transformed centerline 插值决定。 ");
            if (string.Equals(path.Kind, "ZAxis", StringComparison.OrdinalIgnoreCase))
                result.Warnings.Add("ZAxis 仅表示全局 Z 分布方向；钢筋自身方向仍由首末控制筋几何插值决定。 ");
            if (startStyle == RebarStyle.StirrupTie)
                result.Warnings.Add("控制筋为 StirrupTie；GR01 会在创建后把 FreeFormAccessor.RebarStyle 写回 StirrupTie，并在提交前 readback。 ");
            return state;
        }

        private static string RebuildFingerprintOnly(Document doc, RebarGradientSetRequest request)
        {
            var temp = CreateBaseResult(request, string.IsNullOrWhiteSpace(request.OperationId) ? "fingerprint" : request.OperationId);
            string expected = request.ExpectedPlanFingerprint;
            request.ExpectedPlanFingerprint = string.Empty;
            try
            {
                BuildState? state = BuildPlan(doc, request, temp);
                if (state == null || !temp.Success)
                    throw new GradientValidationException(temp.ErrorCode, temp.Error);
                return temp.PlanFingerprint;
            }
            finally
            {
                request.ExpectedPlanFingerprint = expected;
            }
        }

        private static void VerifyCreatedSet(
            Document doc,
            BuildState state,
            RevitRebar created,
            double toleranceMm,
            out double maxDeviationMm)
        {
            if (!created.IsRebarFreeForm())
                throw new GradientValidationException("READBACK_NOT_FREEFORM", "创建结果不是 Free Form Rebar。 ");
            if (created.GetHostId() != state.Host.Id)
                throw new GradientValidationException("READBACK_HOST_MISMATCH", "创建结果 Host 与控制筋 Host 不一致。 ");
            if (created.GetTypeId() != state.BarType.Id)
                throw new GradientValidationException("READBACK_BAR_TYPE_MISMATCH", "创建结果 RebarBarType 与控制筋不一致。 ");
            if (created.NumberOfBarPositions != state.PlannedBars.Count)
                throw new GradientValidationException("READBACK_BAR_COUNT_MISMATCH", $"创建结果 physical bars={created.NumberOfBarPositions}，计划={state.PlannedBars.Count}。 ");

            using (RebarFreeFormAccessor accessor = created.GetFreeFormAccessor())
            {
                if (!accessor.IsUnconstrained())
                    throw new GradientValidationException("READBACK_CONSTRAINT_MODE_MISMATCH", "Explicit ManagedFreeForm 创建后不是 unconstrained。 ");
                if (accessor.HasValidServer())
                    throw new GradientValidationException("READBACK_SERVER_UNEXPECTED", "Explicit ManagedFreeForm 创建后意外绑定了 RebarUpdateServer。 ");
#if !REVIT2021
                if (accessor.RebarStyle != state.Style)
                    throw new GradientValidationException("READBACK_STYLE_MISMATCH", $"创建结果 RebarStyle={accessor.RebarStyle}，控制筋={state.Style}。 ");
#else
                throw new GradientValidationException("FREEFORM_STYLE_API_UNSUPPORTED_REVIT2021", "Revit 2021 无法确认自由形式钢筋样式，拒绝提交。 ");
#endif
            }

            double maxDeviation = 0.0;
            for (int i = 0; i < state.PlannedBars.Count; i++)
            {
                if (!DoesBarExist(created, i))
                    throw new GradientValidationException("READBACK_BAR_POSITION_MISSING", $"创建结果 Bar Position {i} 不存在。 ");
                List<CurveDescriptor> actual = DescribeCurves(GetActualPositionCenterlineCurves(created, i));
                double deviation = ComputeGeometryDeviationMm(state.PlannedBars[i], actual);
                if (double.IsInfinity(deviation) || deviation > toleranceMm)
                    throw new GradientValidationException("READBACK_GEOMETRY_MISMATCH", $"Bar Position {i} 几何偏差 {deviation:0.######}mm 超过容差 {toleranceMm:0.###}mm。 ");
                if (deviation > maxDeviation) maxDeviation = deviation;
            }
            maxDeviationMm = maxDeviation;
        }

        private static void VerifyControlsUnchanged(BuildState state, double toleranceMm)
        {
            if (state.StartRebar.GetHostId() != state.Host.Id || state.EndRebar.GetHostId() != state.Host.Id)
                throw new GradientValidationException("CONTROL_CHANGED_DURING_CREATE", "创建过程中控制筋 Host 发生变化。 ");
            if (state.StartRebar.GetTypeId() != state.BarType.Id || state.EndRebar.GetTypeId() != state.BarType.Id)
                throw new GradientValidationException("CONTROL_CHANGED_DURING_CREATE", "创建过程中控制筋 BarType 发生变化。 ");

            double startDeviation = ComputeGeometryDeviationMm(
                state.StartCurves,
                DescribeCurves(GetActualPositionCenterlineCurves(state.StartRebar, 0)));
            double endDeviation = ComputeGeometryDeviationMm(
                state.EndCurves,
                NormalizeEndControlForReadback(state.StartCurves, DescribeCurves(GetActualPositionCenterlineCurves(state.EndRebar, 0))));
            if (startDeviation > toleranceMm || endDeviation > toleranceMm)
                throw new GradientValidationException("CONTROL_CHANGED_DURING_CREATE", "创建过程中控制筋几何发生变化。 ");
        }

        private static List<CurveDescriptor> NormalizeEndControlForReadback(
            List<CurveDescriptor> start,
            List<CurveDescriptor> end)
        {
            bool forward = TryMatchScore(start, end, out double forwardScore);
            List<CurveDescriptor> reversed = ReverseSequence(end);
            bool reverse = TryMatchScore(start, reversed, out double reverseScore);
            if (reverse && (!forward || reverseScore + 1e-9 < forwardScore)) return reversed;
            return end;
        }

        private static void ValidateRequestSettings(RebarGradientSetRequest request)
        {
            if (request.DistributionPath == null)
                throw new GradientValidationException("MISSING_DISTRIBUTION_PATH", "DistributionPath 不能为空。 ");
            if (request.Zones == null || request.Zones.Count == 0)
                throw new GradientValidationException("EMPTY_ZONES", "Zones 不能为空。 ");
            if (!IsFinitePositive(request.PathProjectionToleranceMm))
                throw new GradientValidationException("INVALID_PATH_PROJECTION_TOLERANCE", "PathProjectionToleranceMm 必须大于 0。 ");
            if (!IsFinitePositive(request.CurveContinuityToleranceMm))
                throw new GradientValidationException("INVALID_CURVE_CONTINUITY_TOLERANCE", "CurveContinuityToleranceMm 必须大于 0。 ");
            if (!IsFinitePositive(request.GeometryReadbackToleranceMm))
                throw new GradientValidationException("INVALID_GEOMETRY_READBACK_TOLERANCE", "GeometryReadbackToleranceMm 必须大于 0。 ");
            if (request.MaxCurvesPerBar < 1)
                throw new GradientValidationException("INVALID_CURVE_LIMIT", "MaxCurvesPerBar 必须大于 0。 ");
        }

        private static List<RebarGradientSpacingZoneInput> NormalizeZonesForDistribution(
            IList<RebarGradientSpacingZoneInput> zones,
            double distributionLengthMm,
            double lengthToleranceMm)
        {
            if (zones == null || zones.Count == 0)
                throw new GradientValidationException("EMPTY_ZONES", "Zones 不能为空。 ");

            int autoCount = zones.Count(zone => zone != null && zone.AutoLength);
            if (autoCount > 1)
                throw new GradientValidationException("MULTIPLE_AUTO_LENGTH_ZONES", "最多只能有一个自动剩余长度区段。 ");

            double fixedTotal = 0.0;
            for (int i = 0; i < zones.Count; i++)
            {
                RebarGradientSpacingZoneInput? zone = zones[i];
                if (zone == null)
                    throw new GradientValidationException("NULL_ZONE", $"Zones[{i}] 不能为空。 ");
                if (!zone.AutoLength)
                {
                    if (!IsFinitePositive(zone.LengthMm))
                        throw new GradientValidationException("INVALID_ZONE_LENGTH", $"Zones[{i}].LengthMm 必须大于 0。 ");
                    fixedTotal += zone.LengthMm;
                }
            }

            if (autoCount == 1 && fixedTotal > distributionLengthMm + lengthToleranceMm)
            {
                throw new GradientValidationException(
                    "AUTO_ZONE_OVER_ALLOCATED",
                    $"固定区段合计 {fixedTotal:0.###}mm 已超过有效路径 {distributionLengthMm:0.###}mm，自动剩余区无法计算。 ");
            }

            double remaining = distributionLengthMm - fixedTotal;
            if (autoCount == 1 && remaining < MinCurveLengthMm)
            {
                throw new GradientValidationException(
                    "AUTO_ZONE_REMAINDER_TOO_SHORT",
                    $"自动剩余区仅 {remaining:0.###}mm，小于最小有效长度 {MinCurveLengthMm:0.###}mm。 ");
            }

            var normalized = new List<RebarGradientSpacingZoneInput>(zones.Count);
            foreach (RebarGradientSpacingZoneInput zone in zones)
            {
                normalized.Add(new RebarGradientSpacingZoneInput
                {
                    Label = zone.Label ?? string.Empty,
                    AutoLength = false,
                    LengthMm = zone.AutoLength ? remaining : zone.LengthMm,
                    MaxSpacingMm = zone.MaxSpacingMm,
                    ManualActualSpacingMm = zone.ManualActualSpacingMm
                });
            }
            return normalized;
        }

        private static PathState ResolveDistributionPath(
            Document doc,
            RebarGradientDistributionPathInput input,
            XYZ startCenter,
            XYZ endCenter)
        {
            string kind = (input.Kind ?? string.Empty).Trim();
            if (kind.Equals("ZAxis", StringComparison.OrdinalIgnoreCase))
            {
                double minZ = Math.Min(startCenter.Z, endCenter.Z);
                double maxZ = Math.Max(startCenter.Z, endCenter.Z);
                double length = maxZ - minZ;
                if (ToMillimeters(length) < MinCurveLengthMm)
                    throw new GradientValidationException("DISTRIBUTION_LENGTH_TOO_SHORT", "两根控制筋沿全局 Z 轴的有效距离必须 >= 1mm。 ");
                return new PathState
                {
                    Kind = "ZAxis",
                    ElementId = null,
                    UniqueId = string.Empty,
                    StableReference = string.Empty,
                    RawStart = new XYZ(0.0, 0.0, minZ),
                    RawEnd = new XYZ(0.0, 0.0, maxZ),
                    UnitDirection = XYZ.BasisZ,
                    RawLengthInternal = length
                };
            }

            if (kind.Equals("ModelLine", StringComparison.OrdinalIgnoreCase))
            {
                if (!input.ElementId.HasValue || input.ElementId.Value <= 0)
                    throw new GradientValidationException("INVALID_MODEL_LINE_ID", "ModelLine 路径需要有效 ElementId。 ");
                Element element = doc.GetElement(ElementIdExtensions.CreateId(input.ElementId.Value))
                    ?? throw new GradientValidationException("MODEL_LINE_NOT_FOUND", $"找不到 DistributionPath ElementId={input.ElementId.Value}。 ");
                if (element is not CurveElement curveElement || element.ViewSpecific)
                    throw new GradientValidationException("PATH_NOT_MODEL_LINE", "DistributionPath Element 必须是非 ViewSpecific 的 Model Curve。 ");
                if (curveElement.GeometryCurve is not Line line)
                    throw new GradientValidationException("PATH_NOT_STRAIGHT", "GR01 V1 DistributionPath 只支持直线 ModelLine。 ");
                return BuildPathState("ModelLine", element.Id.GetIdValue(), element.UniqueId ?? string.Empty, string.Empty, line);
            }

            if (kind.Equals("Edge", StringComparison.OrdinalIgnoreCase))
            {
                string stable = (input.StableReference ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(stable))
                    throw new GradientValidationException("MISSING_EDGE_REFERENCE", "Edge 路径需要 StableReference。 ");
                Reference reference;
                try { reference = Reference.ParseFromStableRepresentation(doc, stable); }
                catch (Exception ex) { throw new GradientValidationException("INVALID_EDGE_REFERENCE", "无法解析 Edge StableReference：" + ex.Message); }
                Element owner = doc.GetElement(reference.ElementId)
                    ?? throw new GradientValidationException("EDGE_OWNER_NOT_FOUND", "找不到 Edge 所属 Element。 ");
                GeometryObject geometry = owner.GetGeometryObjectFromReference(reference);
                if (geometry is not Edge edge)
                    throw new GradientValidationException("REFERENCE_NOT_EDGE", "StableReference 不是有效模型 Edge。 ");
                Curve edgeCurve = edge.AsCurve();
                if (edgeCurve is not Line line)
                    throw new GradientValidationException("PATH_NOT_STRAIGHT", "GR01 V1 DistributionPath 只支持直线 Edge。 ");
                return BuildPathState("Edge", owner.Id.GetIdValue(), owner.UniqueId ?? string.Empty, stable, line);
            }

            throw new GradientValidationException("UNSUPPORTED_PATH_KIND", "DistributionPath.Kind 仅支持 ZAxis / ModelLine / Edge。 ");
        }

        private static PathState BuildPathState(string kind, long elementId, string uniqueId, string stable, Line line)
        {
            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);
            XYZ delta = end - start;
            double length = delta.GetLength();
            if (ToMillimeters(length) < MinCurveLengthMm)
                throw new GradientValidationException("PATH_TOO_SHORT", "DistributionPath 长度必须 >= 1mm。 ");
            return new PathState
            {
                Kind = kind,
                ElementId = elementId,
                UniqueId = uniqueId,
                StableReference = stable,
                RawStart = start,
                RawEnd = end,
                UnitDirection = delta.Normalize(),
                RawLengthInternal = length
            };
        }

        private static void RejectUnsupportedEndFeatures(RevitRebar rebar, string label)
        {
            for (int end = 0; end < 2; end++)
            {
                try
                {
                    ElementId hook = rebar.GetHookTypeId(end);
                    if (hook != ElementId.InvalidElementId)
                        throw new GradientValidationException("CONTROL_HOOK_UNSUPPORTED", $"{label} 存在 Hook；GR01 V1 在无损复制验证前明确拒绝。 ");
                }
                catch (GradientValidationException) { throw; }
                catch { }

                ElementId? termination = InvokeElementId(rebar, "GetEndTreatmentTypeId", end);
                if (termination != null && termination != ElementId.InvalidElementId)
                    throw new GradientValidationException("CONTROL_TERMINATION_UNSUPPORTED", $"{label} 存在 End Treatment；GR01 V1 在无损复制验证前明确拒绝。 ");

                ElementId? coupler = InvokeElementId(rebar, "GetCouplerId", end);
                if (coupler != null && coupler != ElementId.InvalidElementId)
                    throw new GradientValidationException("CONTROL_COUPLER_UNSUPPORTED", $"{label} 存在 Coupler；GR01 V1 不复制 Coupler。 ");
            }
        }

        private static RebarStyle ReadRebarStyle(Document doc, RevitRebar rebar)
        {
            if (rebar.IsRebarFreeForm())
            {
#if REVIT2021
                throw new GradientValidationException("CONTROL_STYLE_UNREADABLE_REVIT2021", "Revit 2021 无法通过 FreeFormAccessor 读取控制筋样式。 ");
#else
                using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                    return accessor.RebarStyle;
#endif
            }
            ElementId shapeId = rebar.GetShapeId();
            if (doc.GetElement(shapeId) is RebarShape shape)
                return shape.RebarStyle;
            throw new GradientValidationException("CONTROL_STYLE_UNREADABLE", $"无法读取 Rebar {rebar.Id.GetIdValue()} 的 RebarStyle。 ");
        }

        private static List<CurveDescriptor> DescribeCurves(IList<Curve> curves)
        {
            var result = new List<CurveDescriptor>();
            foreach (Curve curve in curves)
            {
                if (curve is Line)
                {
                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);
                    result.Add(new CurveDescriptor
                    {
                        Kind = "Line",
                        Start = start,
                        Mid = (start + end) * 0.5,
                        End = end,
                        LengthInternal = curve.Length
                    });
                    continue;
                }
                if (curve is Arc)
                {
                    result.Add(new CurveDescriptor
                    {
                        Kind = "Arc",
                        Start = curve.GetEndPoint(0),
                        Mid = curve.Evaluate(0.5, true),
                        End = curve.GetEndPoint(1),
                        LengthInternal = curve.Length
                    });
                    continue;
                }
                throw new GradientValidationException("UNSUPPORTED_CONTROL_CURVE", $"GR01 V1 控制筋只支持 Line/Arc；检测到 {curve.GetType().Name}。 ");
            }
            if (result.Count == 0)
                throw new GradientValidationException("EMPTY_CONTROL_GEOMETRY", "控制筋没有可用中心线曲线。 ");
            return result;
        }

        private static void ValidateCurveSequence(List<CurveDescriptor> curves, RebarGradientSetRequest request, string label)
        {
            double continuityTolerance = ToInternal(request.CurveContinuityToleranceMm);
            for (int i = 0; i < curves.Count; i++)
            {
                if (ToMillimeters(curves[i].LengthInternal) < MinCurveLengthMm)
                    throw new GradientValidationException("CONTROL_CURVE_TOO_SHORT", $"{label} Curve[{i}] 长度必须 >= {MinCurveLengthMm}mm。 ");
                if (i > 0 && curves[i - 1].End.DistanceTo(curves[i].Start) > continuityTolerance)
                    throw new GradientValidationException("CONTROL_CURVES_NOT_CONTINUOUS", $"{label} Curve[{i - 1}]→Curve[{i}] 不连续（容差 {request.CurveContinuityToleranceMm:0.###}mm）。 ");
            }
        }

        private static bool TryMatchScore(List<CurveDescriptor> start, List<CurveDescriptor> end, out double score)
        {
            score = double.PositiveInfinity;
            if (start.Count != end.Count) return false;
            double total = 0.0;
            for (int i = 0; i < start.Count; i++)
            {
                if (!string.Equals(start[i].Kind, end[i].Kind, StringComparison.Ordinal)) return false;
                total += start[i].Start.DistanceTo(end[i].Start);
                total += start[i].Mid.DistanceTo(end[i].Mid);
                total += start[i].End.DistanceTo(end[i].End);
            }
            score = total;
            return true;
        }

        private static List<CurveDescriptor> ReverseSequence(List<CurveDescriptor> source)
        {
            var result = new List<CurveDescriptor>(source.Count);
            for (int i = source.Count - 1; i >= 0; i--)
                result.Add(source[i].Reversed());
            return result;
        }

        private static XYZ ComputeGeometryCenter(List<CurveDescriptor> curves)
        {
            double weight = 0.0;
            XYZ sum = XYZ.Zero;
            foreach (CurveDescriptor curve in curves)
            {
                double w = Math.Max(curve.LengthInternal, 1e-12);
                sum += curve.Mid * w;
                weight += w;
            }
            return weight <= 0.0 ? curves[0].Mid : sum / weight;
        }

        private static List<CurveDescriptor> InterpolateBar(
            List<CurveDescriptor> start,
            List<CurveDescriptor> end,
            double t,
            RebarGradientSetRequest request)
        {
            var result = new List<CurveDescriptor>(start.Count);
            for (int i = 0; i < start.Count; i++)
            {
                XYZ p0 = Lerp(start[i].Start, end[i].Start, t);
                XYZ pm = Lerp(start[i].Mid, end[i].Mid, t);
                XYZ p1 = Lerp(start[i].End, end[i].End, t);
                string kind = start[i].Kind;
                if (kind == "Line")
                {
                    double len = p0.DistanceTo(p1);
                    if (ToMillimeters(len) < MinCurveLengthMm)
                        throw new GradientValidationException("INTERPOLATED_LINE_TOO_SHORT", $"t={t:0.######} Curve[{i}] Line 长度 <1mm。 ");
                    result.Add(new CurveDescriptor { Kind = kind, Start = p0, Mid = (p0 + p1) * 0.5, End = p1, LengthInternal = len });
                    continue;
                }
                if (kind == "Arc")
                {
                    ValidateArcTriplet(p0, pm, p1, t, i);
                    Arc arc;
                    try { arc = Arc.Create(p0, p1, pm); }
                    catch (Exception ex)
                    {
                        throw new GradientValidationException("INTERPOLATED_ARC_INVALID", $"t={t:0.######} Curve[{i}] Arc 无法创建：{ex.Message}");
                    }
                    result.Add(new CurveDescriptor { Kind = kind, Start = p0, Mid = pm, End = p1, LengthInternal = arc.Length });
                    try { arc.Dispose(); } catch { }
                    continue;
                }
                throw new GradientValidationException("UNSUPPORTED_CONTROL_CURVE", "出现未支持 Curve 类型：" + kind);
            }

            double continuityTolerance = ToInternal(request.CurveContinuityToleranceMm);
            for (int i = 1; i < result.Count; i++)
            {
                if (result[i - 1].End.DistanceTo(result[i].Start) > continuityTolerance)
                    throw new GradientValidationException("INTERPOLATED_CURVES_NOT_CONTINUOUS", $"t={t:0.######} 插值结果 Curve[{i - 1}]→Curve[{i}] 不连续。 ");
            }
            return result;
        }

        private static void ValidateArcTriplet(XYZ start, XYZ mid, XYZ end, double t, int index)
        {
            XYZ a = mid - start;
            XYZ b = end - start;
            double scale = Math.Max(a.GetLength() * b.GetLength(), 1e-16);
            double ratio = a.CrossProduct(b).GetLength() / scale;
            if (start.DistanceTo(mid) < ToInternal(MinCurveLengthMm) ||
                mid.DistanceTo(end) < ToInternal(MinCurveLengthMm) ||
                start.DistanceTo(end) < ToInternal(MinCurveLengthMm) ||
                ratio < ArcCollinearRatioTolerance)
            {
                throw new GradientValidationException("INTERPOLATED_ARC_DEGENERATE", $"t={t:0.######} Curve[{index}] Arc 三点重合或近似共线。 ");
            }
        }

        private static IList<IList<Curve>> BuildRevitCurves(List<List<CurveDescriptor>> bars)
        {
            var result = new List<IList<Curve>>(bars.Count);
            foreach (List<CurveDescriptor> bar in bars)
            {
                var curves = new List<Curve>(bar.Count);
                foreach (CurveDescriptor descriptor in bar)
                {
                    if (descriptor.Kind == "Line")
                        curves.Add(Line.CreateBound(descriptor.Start, descriptor.End));
                    else if (descriptor.Kind == "Arc")
                        curves.Add(Arc.Create(descriptor.Start, descriptor.End, descriptor.Mid));
                    else
                        throw new GradientValidationException("UNSUPPORTED_PLANNED_CURVE", "计划几何包含未支持类型：" + descriptor.Kind);
                }
                result.Add(curves);
            }
            return result;
        }

        private static RebarGradientBarSample BuildBarSample(int index, RebarGradientSpacingPlanResult spacing, List<CurveDescriptor> curves)
        {
            double station = spacing.StationsMm[index];
            double t = spacing.DistributionLengthMm <= 0 ? 0.0 : station / spacing.DistributionLengthMm;
            var sample = new RebarGradientBarSample
            {
                Index = index,
                StationMm = Math.Round(station, 6),
                T = Math.Round(t, 9)
            };
            for (int i = 0; i < curves.Count; i++)
            {
                sample.Curves.Add(new RebarGradientCurveSummary
                {
                    Index = i,
                    CurveType = curves[i].Kind,
                    StartMm = ToPointMm(curves[i].Start),
                    MidMm = ToPointMm(curves[i].Mid),
                    EndMm = ToPointMm(curves[i].End)
                });
            }
            return sample;
        }

        private static double ComputeGeometryDeviationMm(List<CurveDescriptor> planned, List<CurveDescriptor> actual)
        {
            double forward = ComputeDeviationInternal(planned, actual);
            List<CurveDescriptor> reversed = ReverseSequence(actual);
            double reverse = ComputeDeviationInternal(planned, reversed);
            double best = Math.Min(forward, reverse);
            return double.IsInfinity(best) ? double.PositiveInfinity : ToMillimeters(best);
        }

        private static double ComputeDeviationInternal(List<CurveDescriptor> planned, List<CurveDescriptor> actual)
        {
            if (planned.Count != actual.Count) return double.PositiveInfinity;
            double max = 0.0;
            for (int i = 0; i < planned.Count; i++)
            {
                if (!string.Equals(planned[i].Kind, actual[i].Kind, StringComparison.Ordinal))
                    return double.PositiveInfinity;
                max = Math.Max(max, planned[i].Start.DistanceTo(actual[i].Start));
                max = Math.Max(max, planned[i].Mid.DistanceTo(actual[i].Mid));
                max = Math.Max(max, planned[i].End.DistanceTo(actual[i].End));
            }
            return max;
        }

        private static string ComputePlanFingerprint(RebarGradientSetRequest request, RebarGradientSetResult result, BuildState state)
        {
            var builder = new StringBuilder();
            builder.Append("GR01.V1.2|UNIFORM_WITHIN_MAX|");
            builder.Append(result.StartControlUniqueId).Append('|');
            builder.Append(result.EndControlUniqueId).Append('|');
            builder.Append(result.HostUniqueId).Append('|');
            builder.Append(result.BarTypeId).Append('|').Append(result.RebarStyle).Append('|');
            builder.Append(result.DistributionPathKind).Append('|');
            builder.Append(result.DistributionPathUniqueId).Append('|');
            builder.Append(result.DistributionPathStableReference).Append('|');
            AppendPoint(builder, state.Path.RawStart);
            AppendPoint(builder, state.Path.RawEnd);
            builder.Append(result.EndControlReversed).Append('|').Append(result.PathReversed).Append('|');
            AppendDescriptors(builder, state.StartCurves);
            AppendDescriptors(builder, state.EndCurves);
            builder.Append(F(request.LengthToleranceMm)).Append('|');
            builder.Append(F(request.StationMergeToleranceMm)).Append('|');
            for (int zoneIndex = 0; zoneIndex < request.Zones.Count; zoneIndex++)
            {
                RebarGradientSpacingZoneInput zone = request.Zones[zoneIndex];
                double fingerprintLength = zone.AutoLength && zoneIndex < state.SpacingPlan.Zones.Count
                    ? state.SpacingPlan.Zones[zoneIndex].LengthMm
                    : zone.LengthMm;
                builder.Append(zone.Label ?? string.Empty).Append(':')
                    .Append(zone.AutoLength ? "AUTO" : "FIXED").Append(':')
                    .Append(F(fingerprintLength)).Append(':')
                    .Append(F(zone.MaxSpacingMm)).Append(':')
                    .Append(zone.ManualActualSpacingMm.HasValue ? F(zone.ManualActualSpacingMm.Value) : "-")
                    .Append('|');
            }
            foreach (double station in state.SpacingPlan.StationsMm)
                builder.Append(F(station)).Append(',');

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }

        private static void AppendDescriptors(StringBuilder builder, IEnumerable<CurveDescriptor> curves)
        {
            foreach (CurveDescriptor curve in curves)
            {
                builder.Append(curve.Kind).Append('[');
                AppendPoint(builder, curve.Start);
                AppendPoint(builder, curve.Mid);
                AppendPoint(builder, curve.End);
                builder.Append(']');
            }
            builder.Append('|');
        }

        private static void AppendPoint(StringBuilder builder, XYZ p)
        {
            builder.Append(F(ToMillimeters(p.X))).Append(',')
                .Append(F(ToMillimeters(p.Y))).Append(',')
                .Append(F(ToMillimeters(p.Z))).Append(';');
        }

        private static string F(double value) => Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

        private static RebarGradientSetResult CreateBaseResult(RebarGradientSetRequest request, string operationId)
        {
            return new RebarGradientSetResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                OperationId = operationId,
                StartControlRebarId = request.StartControlRebarId,
                EndControlRebarId = request.EndControlRebarId,
                EngineUsed = "ExplicitManagedFreeForm",
                ConstraintMode = "ManagedGeometry",
                NativeToCover = false
            };
        }

        private static IList<Curve> GetActualPositionCenterlineCurves(RevitRebar rebar, int position)
        {
            try
            {
                MethodInfo? transformed = rebar.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "GetTransformedCenterlineCurves" && method.GetParameters().Length == 5);
                if (transformed != null)
                {
                    object? raw = transformed.Invoke(rebar, new object[]
                    {
                        false,
                        false,
                        false,
                        MultiplanarOption.IncludeAllMultiplanarCurves,
                        position
                    });
                    if (raw is IList<Curve> list) return list;
                    if (raw is IEnumerable<Curve> enumerable) return enumerable.ToList();
                }
            }
            catch { }

            return rebar.GetCenterlineCurves(
                false,
                false, // Revit 2026+ renamed this argument suppressHooksAndCranks.
                false,
                MultiplanarOption.IncludeAllMultiplanarCurves,
                position);
        }

        private static bool DoesBarExist(RevitRebar rebar, int index)
        {
            try
            {
                MethodInfo? method = rebar.GetType().GetMethod("DoesBarExistAtPosition", BindingFlags.Instance | BindingFlags.Public);
                if (method?.Invoke(rebar, new object[] { index }) is bool exists) return exists;
            }
            catch { }
            return true;
        }

        private static ElementId? InvokeElementId(object target, string methodName, params object[] args)
        {
            try
            {
                MethodInfo? method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
                return method?.Invoke(target, args) as ElementId;
            }
            catch { return null; }
        }

        private static XYZ Lerp(XYZ a, XYZ b, double t) => a + (b - a) * t;

        private static double ToInternal(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        private static double ToMillimeters(double internalValue) => UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);

        private static RebarPointMm ToPointMm(XYZ point) => new RebarPointMm
        {
            X = Math.Round(ToMillimeters(point.X), 6),
            Y = Math.Round(ToMillimeters(point.Y), 6),
            Z = Math.Round(ToMillimeters(point.Z), 6)
        };

        private static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;

        private sealed class GradientValidationException : Exception
        {
            public string Code { get; }
            public GradientValidationException(string code, string message) : base(message)
            {
                Code = code;
            }
        }
    }
}
