using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.CadToRevit
{
    /// <summary>
    /// 仅服务于 CAD->Revit 详图线事务。遇到 Revit 原生 CannotKeepJoined 系列错误时，
    /// 只有失败图元全部属于本事务新创建的 DetailCurve，且 Revit 支持 DetachElements
    /// 才尝试一次断开约束。任何未知错误/无法解析/再次出现的错误均回滚。
    /// 不删除警告、不删除模型图元、不修改宿主现有线段。
    /// </summary>
    internal sealed class CadToRevitJoinFailurePreprocessor : IFailuresPreprocessor
    {
        private readonly Document _document;
        private readonly HashSet<long> _createdIds = new HashSet<long>();
        private readonly Dictionary<long, ExpectedCurve> _sourceCurves = new Dictionary<long, ExpectedCurve>();
        private readonly List<string> _diagnostics = new List<string>();
        private bool _attempted;
        // 严格验收阈值保持 P4 的 1e-6 ft；仅在本事务实际解除 Join 约束后，
        // 才允许有限的 Revit 自动端点吸附，并向逐源审计记录每一条偏移。
        private readonly List<string> _acceptedAutojoinDrifts = new List<string>();
        private double _maxAcceptedDriftFeet;
        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double MaxAutojoinDriftMillimeters = 0.15;

        public CadToRevitJoinFailurePreprocessor(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public int ResolvedCount { get; private set; }
        public int AcceptedAutojoinDriftCount => _acceptedAutojoinDrifts.Count;
        public double MaxAcceptedAutojoinDriftMillimeters => _maxAcceptedDriftFeet / FeetPerMillimeter;
        public IReadOnlyList<string> AcceptedAutojoinDriftDetails => _acceptedAutojoinDrifts.AsReadOnly();
        public string Summary => _diagnostics.Count == 0 ? "无连接失败" : string.Join(" | ", _diagnostics);

        public void TrackCommittedSourceCurves(string sourceId, IList<CadToRevitElementTrace> created, IList<Curve> sourceGeometry)
        {
            if (created.Count != sourceGeometry.Count)
                throw new InvalidOperationException("CAD 线条几何与 ElementId 对应数量不一致：" + sourceId);
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i].Role != "detail_curve" || !_createdIds.Add(created[i].ElementId))
                    throw new InvalidOperationException("CAD 线条创建结果重复或类型错误：" + sourceId);
                _sourceCurves[created[i].ElementId] = new ExpectedCurve { SourceId = sourceId, Geometry = sourceGeometry[i] };
            }
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            IList<FailureMessageAccessor> messages = accessor.GetFailureMessages().ToList();
            // DocumentCorruption: API 禁止读取文档；直接回滚，不能尝试访问失败 Element。
            if (messages.Any(x => x.GetSeverity() == FailureSeverity.DocumentCorruption))
            {
                _diagnostics.Add("Revit 报告文档损坏级别错误，强制回滚；未读取模型内容。");
                return FailureProcessingResult.ProceedWithRollBack;
            }
            IList<FailureMessageAccessor> errors = messages
                .Where(x => x.GetSeverity() == FailureSeverity.Error).ToList();
            if (errors.Count == 0) return FailureProcessingResult.Continue; // 保留普通警告原生处理

            // 不允许自动解决第二轮仍出现的连接错误，防止 ProceedWithCommit 无限循环。
            if (_attempted)
            {
                RecordFailures("第二轮仍存在错误，停止重试", errors);
                return FailureProcessingResult.ProceedWithRollBack;
            }

            // 先验证本轮全部错误：不能解决部分错误后留下一个未知错误。
            foreach (FailureMessageAccessor failure in errors)
            {
                ICollection<ElementId> failingIds = failure.GetFailingElementIds();
                bool onlyNewDetailLines = failingIds != null && failingIds.Count >= 2 && failingIds.All(id =>
                    _createdIds.Contains(id.GetIdValue()) && _document.GetElement(id) is DetailCurve);
                if (!IsKnownJoinFailure(failure.GetFailureDefinitionId()) ||
                    failure.GetSeverity() != FailureSeverity.Error ||
                    !onlyNewDetailLines ||
                    !failure.HasResolutionOfType(FailureResolutionType.DetachElements) ||
                    !accessor.IsFailureResolutionPermitted(failure, FailureResolutionType.DetachElements))
                {
                    RecordFailures("不满足仅对新建详图线安全解耦的条件", errors);
                    return FailureProcessingResult.ProceedWithRollBack;
                }
            }

            _attempted = true;
            try
            {
                foreach (FailureMessageAccessor failure in errors)
                {
                    failure.SetCurrentResolutionType(FailureResolutionType.DetachElements);
                    accessor.ResolveFailure(failure);
                    ResolvedCount++;
                }
                _diagnostics.Add("尝试 DetachElements: " + ResolvedCount + " 处（提交后需读回全部 ID）");
                return FailureProcessingResult.ProceedWithCommit;
            }
            catch (Exception ex)
            {
                _diagnostics.Add("解除连接失败：" + ex.GetType().Name + " " + ex.Message);
                return FailureProcessingResult.ProceedWithRollBack;
            }
        }

        public void AssertCreatedCurvesStillExist()
        {
            _acceptedAutojoinDrifts.Clear();
            _maxAcceptedDriftFeet = 0.0;
            var mismatch = new List<string>();
            foreach (KeyValuePair<long, ExpectedCurve> entry in _sourceCurves)
            {
                DetailCurve current = _document.GetElement(ToElementId(entry.Key)) as DetailCurve;
                if (current == null)
                {
                    mismatch.Add(entry.Value.SourceId + ":" + entry.Key + "(图元缺失)");
                    continue;
                }
                Curve actual = current.GeometryCurve;
                Curve expected = entry.Value.Geometry;
                if (SameGeometry(actual, expected)) continue; // P4 严格几何核验不变

                // 不允许无条件放宽：必须是本次新建详图线，且已经实际处理过 Join Failure。
                if (ResolvedCount > 0 && TryAcceptAutojoinDrift(actual, expected,
                    _document.Application.ShortCurveTolerance, out double driftFeet))
                {
                    _maxAcceptedDriftFeet = Math.Max(_maxAcceptedDriftFeet, driftFeet);
                    _acceptedAutojoinDrifts.Add("SourceId=" + entry.Value.SourceId +
                        "; ElementId=" + entry.Key + "; MaxDriftMm=" +
                        (driftFeet / FeetPerMillimeter).ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                    continue;
                }
                mismatch.Add(entry.Value.SourceId + ":" + entry.Key + "(几何超限或非受控改变)");
            }
            if (mismatch.Count != 0)
                throw new InvalidOperationException("连接解耦后详图线丢失或几何改变，已阻止提交；SourceId:ElementId=" +
                    string.Join(",", mismatch.Take(30)) +
                    "；已检查 " + _sourceCurves.Count + " 条，受控微调候选 " + _acceptedAutojoinDrifts.Count + " 条。");
        }

        /// <summary>
        /// 只接受 Revit Autojoin 对本次新建、已解除连接的 DetailCurve 所造成的受限微调。
        /// 双重限制：0.15mm 绝对值、宿主最短曲线容差的 20%、源曲线长度的 20%。
        /// 只处理 Line 或有界 Arc；无界整圆继续用 P4 原始严格比较，避免整圆中心漂移。
        /// </summary>
        private static bool TryAcceptAutojoinDrift(Curve actual, Curve expected, double shortCurveTolerance,
            out double maxDriftFeet)
        {
            maxDriftFeet = double.PositiveInfinity;
            if (actual == null || expected == null || actual.GetType() != expected.GetType() ||
                !actual.IsBound || !expected.IsBound || shortCurveTolerance <= 0)
                return false;
            if (!(expected is Line) && !(expected is Arc)) return false;
            double limit = Math.Min(MaxAutojoinDriftMillimeters * FeetPerMillimeter,
                Math.Min(0.20 * shortCurveTolerance, 0.20 * expected.Length));
            if (limit <= 0 || double.IsNaN(limit) || double.IsInfinity(limit)) return false;
            double lengthDelta = Math.Abs(actual.Length - expected.Length);
            if (double.IsNaN(lengthDelta) || lengthDelta > limit) return false;

            double direct = Math.Max(actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(0)),
                                     actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(1)));
            double reverse = Math.Max(actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(1)),
                                      actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(0)));
            double drift = Math.Max(lengthDelta, Math.Min(direct, reverse));
            if (actual is Arc a && expected is Arc b)
            {
                // 保留 P4 的法向/弧中点保护；互补半圆不能被当成微调。
                if (Math.Abs(Math.Abs(a.Normal.DotProduct(b.Normal)) - 1.0) > 1e-9) return false;
                drift = Math.Max(drift, a.Center.DistanceTo(b.Center));
                drift = Math.Max(drift, Math.Abs(a.Radius - b.Radius));
                drift = Math.Max(drift, a.Evaluate(0.5, true).DistanceTo(b.Evaluate(0.5, true)));
            }
            maxDriftFeet = drift;
            return !double.IsNaN(drift) && !double.IsInfinity(drift) && drift <= limit;
        }

        private sealed class ExpectedCurve
        {
            public string SourceId;
            public Curve Geometry;
        }

        private static bool SameGeometry(Curve actual, Curve expected)
        {
            // 源 CAD 曲线须保持原样：曲线类型、边界性质和真实三维几何必须一致。
            // Revit 整圆为非绑定 Arc，没有端点；绝不能直接对它调用 GetEndPoint。
            if (actual == null || expected == null || actual.GetType() != expected.GetType()) return false;
            if (actual.IsBound != expected.IsBound) return false;
            const double epsilonFeet = 1e-6; // 约 0.000305mm，明显小于 Revit 最短边容差。
            if (Math.Abs(actual.Length - expected.Length) > epsilonFeet) return false;

            if (actual is Arc a && expected is Arc b)
            {
                if (a.Center.DistanceTo(b.Center) > epsilonFeet ||
                    Math.Abs(a.Radius - b.Radius) > epsilonFeet)
                    return false;
                // 同心同半径但法向不同（非同一平面）也不是相同几何；反向法线等价。
                if (Math.Abs(Math.Abs(a.Normal.DotProduct(b.Normal)) - 1.0) > 1e-9)
                    return false;
                // 两者已确认同为非绑定整圆：用中心/半径/平面判定，不读取端点。
                if (!actual.IsBound) return true;
                // 有界圆弧还需检查弧中点；仅端点、圆心、长度无法区分两条互补半圆。
                if (actual.Evaluate(0.5, true).DistanceTo(expected.Evaluate(0.5, true)) > epsilonFeet)
                    return false;
            }
            else if (!actual.IsBound)
            {
                // 不支持的其它非绑定曲线不能仅凭 Length 判为相同。
                return false;
            }

            // 只有两条曲线都已确认有界时，才可安全读取端点；正反向等价。
            bool direct = actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(0)) <= epsilonFeet &&
                          actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(1)) <= epsilonFeet;
            bool reversed = actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(1)) <= epsilonFeet &&
                            actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(0)) <= epsilonFeet;
            return direct || reversed;
        }

        private static bool IsKnownJoinFailure(FailureDefinitionId id)
        {
            // 严格白名单；不依据英文/中文错误文本匹配，不吞掉其它约束错误。
            return id != null &&
                   (id.Equals(BuiltInFailures.JoinElementsFailures.CannotKeepJoined) ||
                    id.Equals(BuiltInFailures.JoinElementsFailures.CannotJoinElements) ||
                    id.Equals(BuiltInFailures.JoinElementsFailures.CannotJoinElementsError));
        }

        private static ElementId ToElementId(long id)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(id);
#else
            if (id < int.MinValue || id > int.MaxValue)
                throw new InvalidOperationException("旧版 Revit 不能表示该 ElementId: " + id);
            return new ElementId((int)id);
#endif
        }

        private void RecordFailures(string reason, IEnumerable<FailureMessageAccessor> errors)
        {
            foreach (FailureMessageAccessor failure in errors)
            {
                string ids = string.Join(",", failure.GetFailingElementIds().Select(x => x.GetIdValue()));
                _diagnostics.Add(reason + "；FailureId=" + failure.GetFailureDefinitionId() +
                    "；Severity=" + failure.GetSeverity() + "；Ids=[" + ids +
                    "]；Message=" + failure.GetDescriptionText());
            }
        }
    }
}
