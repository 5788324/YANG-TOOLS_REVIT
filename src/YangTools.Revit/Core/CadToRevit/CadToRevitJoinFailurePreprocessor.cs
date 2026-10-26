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

        public CadToRevitJoinFailurePreprocessor(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public int ResolvedCount { get; private set; }
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
            var mismatch = new List<string>();
            foreach (KeyValuePair<long, ExpectedCurve> entry in _sourceCurves)
            {
                DetailCurve current = _document.GetElement(ToElementId(entry.Key)) as DetailCurve;
                if (current == null || !SameGeometry(current.GeometryCurve, entry.Value.Geometry))
                    mismatch.Add(entry.Value.SourceId + ":" + entry.Key);
            }
            if (mismatch.Count != 0)
                throw new InvalidOperationException("连接解耦后详图线丢失或几何改变，已阻止提交；SourceId:ElementId=" +
                    string.Join(",", mismatch.Take(30)));
        }

        private sealed class ExpectedCurve
        {
            public string SourceId;
            public Curve Geometry;
        }

        private static bool SameGeometry(Curve actual, Curve expected)
        {
            // 原 CAD 曲线是独立的 Line/Arc；端点允许反向，但不得改变长度、圆心或半径。
            if (actual == null || expected == null || actual.GetType() != expected.GetType()) return false;
            const double epsilonFeet = 1e-6; // 约 0.000305mm，明显小于 Revit 最短边容差。
            if (Math.Abs(actual.Length - expected.Length) > epsilonFeet) return false;
            bool direct = actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(0)) <= epsilonFeet &&
                          actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(1)) <= epsilonFeet;
            bool reversed = actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(1)) <= epsilonFeet &&
                            actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(0)) <= epsilonFeet;
            if (!direct && !reversed) return false;
            if (actual is Arc a && expected is Arc b)
                return a.Center.DistanceTo(b.Center) <= epsilonFeet && Math.Abs(a.Radius - b.Radius) <= epsilonFeet;
            return true;
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
