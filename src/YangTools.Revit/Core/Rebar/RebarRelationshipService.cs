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
    /// R06 relationship layer: Shape Driven Rebar -> Shape Driven Rebar constraint.
    /// It only consumes native ToOtherRebar candidates offered by Revit; it never fabricates
    /// geometry or silently guesses between multiple handles/candidates.
    /// </summary>
    public static class RebarRelationshipService
    {
        public static RebarToRebarConstraintResult ConstrainToOtherRebar(
            UIApplication uiapp,
            RebarToRebarConstraintRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Fail(request, "当前没有活动 Revit 文档。");
            Document doc = uidoc.Document;

            var source = doc.GetElement(ElementIdExtensions.CreateId(request.SourceRebarId)) as Autodesk.Revit.DB.Structure.Rebar;
            var target = doc.GetElement(ElementIdExtensions.CreateId(request.TargetRebarId)) as Autodesk.Revit.DB.Structure.Rebar;
            if (source == null) return Fail(request, $"找不到 Source Rebar ElementId={request.SourceRebarId}。");
            if (target == null) return Fail(request, $"找不到 Target Rebar ElementId={request.TargetRebarId}。");
            if (source.Id == target.Id) return Fail(request, "Source 与 Target Rebar 不能是同一元素。");
            if (source.IsRebarFreeForm() || target.IsRebarFreeForm())
                return Fail(request, "R06 ToOtherRebar 当前仅支持 Shape Driven Rebar。");
            if (!string.IsNullOrWhiteSpace(request.ExpectedSourceUniqueId) && source.UniqueId != request.ExpectedSourceUniqueId)
                return Fail(request, "ExpectedSourceUniqueId 与当前 Source Rebar 不一致。");
            if (!string.IsNullOrWhiteSpace(request.ExpectedTargetUniqueId) && target.UniqueId != request.ExpectedTargetUniqueId)
                return Fail(request, "ExpectedTargetUniqueId 与当前 Target Rebar 不一致。");

            if (!Enum.TryParse(request.SourceHandleType ?? string.Empty, true, out RebarHandleType handleType))
                return Fail(request, "SourceHandleType 无效；R06 支持 Edge / StartOfBar / EndOfBar / RebarPlane 等 RebarHandleType。 ");

            RebarInspectionReport before = RebarInspectionService.Inspect(uiapp, new[] { source.Id, target.Id });
            RebarInspectionItem? sourceBefore = before.Items.FirstOrDefault(x => x.ElementId == request.SourceRebarId);
            if (sourceBefore == null) return Fail(request, "无法读取 Source Rebar 快照。", before);

            var result = new RebarToRebarConstraintResult
            {
                Success = true,
                DryRun = request.DryRun,
                ChangedModel = false,
                SourceRebarId = request.SourceRebarId,
                SourceUniqueId = source.UniqueId,
                TargetRebarId = request.TargetRebarId,
                TargetUniqueId = target.UniqueId,
                SourceHandleType = handleType.ToString(),
                SourceHandleOrdinal = request.SourceHandleOrdinal,
                SameSourceElementId = true,
                SameSourceUniqueId = true,
                SameSourceHost = true,
                BeforeInspection = before,
                AfterInspection = before
            };

            long originalHostId = sourceBefore.HostId;
            using (RebarConstraintsManager manager = source.GetRebarConstraintsManager())
            {
                List<RebarConstrainedHandle> handles = GetHandles(manager, handleType);
                result.MatchingSourceHandleCount = handles.Count;
                try
                {
                    if (handles.Count == 0)
                        return Error(result, $"Source Rebar 没有 {handleType} handle。");

                    int selectedOrdinal;
                    if (request.SourceHandleOrdinal.HasValue)
                    {
                        selectedOrdinal = request.SourceHandleOrdinal.Value;
                        if (selectedOrdinal < 0 || selectedOrdinal >= handles.Count)
                            return Error(result, $"SourceHandleOrdinal 超出范围；{handleType} handle 数量={handles.Count}。");
                    }
                    else if (handles.Count == 1)
                    {
                        selectedOrdinal = 0;
                    }
                    else
                    {
                        return Error(result, $"{handleType} handle 数量={handles.Count}，R06 拒绝猜测；请提供 SourceHandleOrdinal。 ");
                    }
                    result.SourceHandleOrdinal = selectedOrdinal;
                    RebarConstrainedHandle handle = handles[selectedOrdinal];

                    // From Revit 2026, the constraint candidates API requires a target ElementId.
#if REVIT2026_OR_GREATER
                    IList<RebarConstraint> raw = manager.GetConstraintCandidatesForHandle(handle, target.Id);
#else
                    IList<RebarConstraint> raw = manager.GetConstraintCandidatesForHandle(handle);
#endif
                    var candidates = new List<RebarConstraint>();
                    try
                    {
                        int ordinal = 0;
                        foreach (RebarConstraint candidate in raw)
                        {
                            if (!IsCandidateForTarget(candidate, target.Id)) continue;
                            if (candidate.GetConstraintType() != RebarConstraintType.ToOtherRebar) continue;
                            var info = BuildCandidate(candidate, target, ordinal++);
                            if (!MatchesTargetSelector(info, request)) continue;
                            result.Candidates.Add(info);
                            candidates.Add(candidate);
                        }
                        result.CandidateCount = result.Candidates.Count;
                        if (result.CandidateCount == 0)
                            return Error(result, "Revit 未提供满足条件的 ToOtherRebar candidate；R06 不会人工伪造关系。 ");

                        if (request.DryRun)
                        {
                            result.Message = result.CandidateCount == 1
                                ? "R06 dryRun：找到唯一 ToOtherRebar candidate；未启动 Transaction。"
                                : $"R06 dryRun：找到 {result.CandidateCount} 个 candidate；应用前需要进一步指定 TargetKind/TargetIndex。";
                            return result;
                        }

                        if (candidates.Count != 1)
                            return Error(result, $"ToOtherRebar candidate 数量={candidates.Count}；R06 拒绝猜测，请用 TargetKind / TargetIndex 缩小到唯一候选。 ");

                        RebarConstraint selected = candidates[0];
                        Transaction? tx = null;
                        try
                        {
                            tx = new Transaction(doc, "YangTools R06 Rebar-to-Rebar 关系");
                            TransactionStatus started = tx.Start();
                            if (started != TransactionStatus.Started)
                                return Error(result, $"无法启动 Revit 事务：{started}");

                            if (request.DistanceMm.HasValue)
                            {
                                double internalDistance = UnitUtils.ConvertToInternalUnits(request.DistanceMm.Value, UnitTypeId.Millimeters);
                                selected.SetDistanceToTargetRebar(internalDistance);
                            }
                            RebarApiCompatibility.SetPreferredConstraint(manager, handle, selected);
                            doc.Regenerate();

                            RebarInspectionReport mid = RebarInspectionService.Inspect(uiapp, new[] { source.Id, target.Id });
                            RebarInspectionItem? sourceAfter = mid.Items.FirstOrDefault(x => x.ElementId == request.SourceRebarId);
                            if (sourceAfter == null) throw new InvalidOperationException("提交前无法读取 Source Rebar。");

                            result.SameSourceElementId = sourceAfter.ElementId == request.SourceRebarId;
                            result.SameSourceUniqueId = sourceAfter.UniqueId == source.UniqueId;
                            result.SameSourceHost = sourceAfter.HostId == originalHostId;
                            RebarConstraintInspection? readback = sourceAfter.Constraints.FirstOrDefault(c =>
                                string.Equals(c.HandleType, handleType.ToString(), StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.ConstraintType, RebarConstraintType.ToOtherRebar.ToString(), StringComparison.OrdinalIgnoreCase) &&
                                c.TargetRebarElementIds.Contains(request.TargetRebarId));
                            result.ToOtherRebarReadback = readback != null;
                            result.ExactTargetRebarReadback = readback != null &&
                                (request.TargetIndex == null || ReadbackMatchesIndex(readback, request));

                            if (!result.SameSourceElementId || !result.SameSourceUniqueId || !result.SameSourceHost ||
                                !result.ToOtherRebarReadback || !result.ExactTargetRebarReadback)
                                throw new InvalidOperationException("R06 readback 不满足身份/宿主/ToOtherRebar 精确目标不变量；事务回滚。");

                            TransactionStatus committed = tx.Commit();
                            result.TransactionStatus = committed.ToString();
                            if (committed != TransactionStatus.Committed)
                                throw new InvalidOperationException($"事务提交状态不是 Committed：{committed}");

                            result.ChangedModel = true;
                            result.AfterInspection = RebarInspectionService.Inspect(uiapp, new[] { source.Id, target.Id });
                            result.Message = "R06 ToOtherRebar 关系已原位建立并完成 R00.5 readback。";
                            return result;
                        }
                        catch (Exception ex)
                        {
                            try { if (tx != null && tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); } catch { }
                            result.Success = false;
                            result.ChangedModel = false;
                            result.Error = ex.Message;
                            result.TransactionStatus = tx?.GetStatus().ToString() ?? string.Empty;
                            result.AfterInspection = RebarInspectionService.Inspect(uiapp, new[] { source.Id, target.Id });
                            return result;
                        }
                        finally { try { tx?.Dispose(); } catch { } }
                    }
                    finally
                    {
                        foreach (RebarConstraint c in raw) try { c.Dispose(); } catch { }
                    }
                }
                finally
                {
                    foreach (RebarConstrainedHandle h in handles) try { h.Dispose(); } catch { }
                }
            }
        }

        private static List<RebarConstrainedHandle> GetHandles(RebarConstraintsManager manager, RebarHandleType type)
        {
            IList<RebarConstrainedHandle> all = manager.GetAllHandles();
            var result = new List<RebarConstrainedHandle>();
            foreach (RebarConstrainedHandle h in all)
            {
                bool match = false;
                try { match = h.GetHandleType() == type; } catch { }
                if (match) result.Add(h); else try { h.Dispose(); } catch { }
            }
            return result;
        }

        private static bool IsCandidateForTarget(RebarConstraint candidate, ElementId targetId)
        {
            // Shape Driven ToOtherRebar constraints are single-target in the workflows R06 accepts.
            // Avoid depending on newer target-count API so this code remains compile-friendly for 2021+.
            try
            {
                Element? target = candidate.GetTargetElement();
                return target != null && target.Id == targetId;
            }
            catch { return false; }
        }

        private static RebarRelationshipCandidate BuildCandidate(RebarConstraint candidate, Autodesk.Revit.DB.Structure.Rebar target, int ordinal)
        {
            var info = new RebarRelationshipCandidate
            {
                CandidateOrdinal = ordinal,
                ConstraintType = candidate.GetConstraintType().ToString(),
                TargetRebarId = target.Id.GetIdValue(),
                TargetRebarUniqueId = target.UniqueId
            };
            try { info.TargetKind = candidate.GetTargetRebarConstraintType().ToString(); } catch { }
            try
            {
                if (candidate.TargetRebarConstraintTypeIsEdge()) info.TargetIndex = candidate.GetTargetRebarEdgeNumber();
                else if (candidate.TargetIsBarBend()) info.TargetIndex = candidate.GetTargetRebarBendNumber();
                else if (candidate.TargetIsHookBend()) info.TargetIndex = candidate.GetTargetRebarHookBarEnd();
            }
            catch { }
            try { info.DistanceMm = Math.Round(UnitUtils.ConvertFromInternalUnits(candidate.GetDistanceToTargetRebar(), UnitTypeId.Millimeters), 3); } catch { }
            return info;
        }

        private static bool MatchesTargetSelector(RebarRelationshipCandidate info, RebarToRebarConstraintRequest request)
        {
            string kind = (request.TargetKind ?? "Any").Trim();
            if (!string.Equals(kind, "Any", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kind, info.TargetKind, StringComparison.OrdinalIgnoreCase))
                return false;
            if (request.TargetIndex.HasValue && info.TargetIndex != request.TargetIndex) return false;
            return true;
        }

        private static bool ReadbackMatchesIndex(RebarConstraintInspection detail, RebarToRebarConstraintRequest request)
        {
            if (!request.TargetIndex.HasValue) return true;
            string kind = (request.TargetKind ?? "Any").Trim();
            if (string.Equals(kind, "Edge", StringComparison.OrdinalIgnoreCase)) return detail.TargetRebarEdgeNumber == request.TargetIndex;
            if (string.Equals(kind, "BarBend", StringComparison.OrdinalIgnoreCase)) return detail.TargetRebarBendNumber == request.TargetIndex;
            if (string.Equals(kind, "HookBend", StringComparison.OrdinalIgnoreCase)) return detail.TargetRebarHookBarEnd == request.TargetIndex;
            return detail.TargetRebarEdgeNumber == request.TargetIndex || detail.TargetRebarBendNumber == request.TargetIndex || detail.TargetRebarHookBarEnd == request.TargetIndex;
        }

        private static RebarToRebarConstraintResult Fail(RebarToRebarConstraintRequest r, string error, RebarInspectionReport? before = null)
        {
            return new RebarToRebarConstraintResult { Success = false, DryRun = r.DryRun, ChangedModel = false, SourceRebarId = r.SourceRebarId, TargetRebarId = r.TargetRebarId, Error = error, BeforeInspection = before, AfterInspection = before };
        }

        private static RebarToRebarConstraintResult Error(RebarToRebarConstraintResult result, string error)
        {
            result.Success = false; result.ChangedModel = false; result.Error = error; return result;
        }
    }
}
