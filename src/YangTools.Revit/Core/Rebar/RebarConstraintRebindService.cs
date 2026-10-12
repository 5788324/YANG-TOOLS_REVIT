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
    /// R05 / M2: retarget existing Shape Driven Rebar host-cover constraints without rebuilding.
    ///
    /// Scope:
    /// - same Rebar element and same Rebar host only;
    /// - optional Edge / StartOfBar / EndOfBar -> exposed host face ToCover retarget;
    /// - planar faces only in R05;
    /// - dryRun candidate preflight;
    /// - verify exact target face via R00.4 stable face-reference readback;
    /// - preserve ElementId, UniqueId, Host, varying mode and every unretargeted constraint.
    ///
    /// R05 does NOT move a rebar to another host, change topology, create Free Form Rebar,
    /// or solve collisions. Those belong to later stages.
    /// </summary>
    public static class RebarConstraintRebindService
    {
        public static ShapeDrivenConstraintRebindResult Execute(
            UIApplication uiapp,
            ShapeDrivenConstraintRebindRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
                return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            Autodesk.Revit.DB.Structure.Rebar? rebar =
                doc.GetElement(ElementIdExtensions.CreateId(request.RebarId)) as Autodesk.Revit.DB.Structure.Rebar;
            if (rebar == null)
                return Failure(request, $"找不到 Rebar ElementId={request.RebarId}。");
            if (rebar.IsRebarFreeForm())
                return Failure(request, "R05 仅支持 Shape Driven Rebar；Free Form 不在本阶段。");

            string originalUniqueId = rebar.UniqueId;
            if (!string.IsNullOrWhiteSpace(request.ExpectedUniqueId) &&
                !string.Equals(request.ExpectedUniqueId, originalUniqueId, StringComparison.Ordinal))
                return Failure(request, "ExpectedUniqueId 与当前 Rebar 不一致；拒绝重绑可能已变化的目标。");

            RebarInspectionReport beforeReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
            RebarInspectionItem? before = beforeReport.Items.FirstOrDefault();
            if (before == null || !string.IsNullOrWhiteSpace(before.Error))
                return Failure(request, "无法读取重绑前 Rebar 快照。", beforeReport);
            if (!string.Equals(before.EngineMode, "ShapeDriven", StringComparison.OrdinalIgnoreCase))
                return Failure(request, $"R05 仅支持 ShapeDriven，当前为 {before.EngineMode}。", beforeReport);

            bool edgeRequested = !string.IsNullOrWhiteSpace(request.EdgeFaceStableReference);
            bool startRequested = !string.IsNullOrWhiteSpace(request.StartFaceStableReference);
            bool endRequested = !string.IsNullOrWhiteSpace(request.EndFaceStableReference);
            if (!edgeRequested && !startRequested && !endRequested)
                return Failure(request, "R05 至少需要提供一个 Edge / Start / End 目标 FaceStableReference。", beforeReport);

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(before.HostId));
            if (host == null || !RebarHostData.IsValidHost(host))
                return Failure(request, "当前 Rebar Host 无效或已不可用。", beforeReport);

            var result = new ShapeDrivenConstraintRebindResult
            {
                Success = true,
                DryRun = request.DryRun,
                ChangedModel = false,
                RebarId = request.RebarId,
                UniqueId = originalUniqueId,
                HostId = before.HostId,
                HostUniqueId = before.HostUniqueId,
                EdgeRequested = edgeRequested,
                StartRequested = startRequested,
                EndRequested = endRequested,
                EdgeTargetFaceStableReference = request.EdgeFaceStableReference?.Trim() ?? string.Empty,
                StartTargetFaceStableReference = request.StartFaceStableReference?.Trim() ?? string.Empty,
                EndTargetFaceStableReference = request.EndFaceStableReference?.Trim() ?? string.Empty,
                BeforeInspection = beforeReport,
                AfterInspection = beforeReport,
                SameElementId = true,
                SameUniqueId = true,
                SameHost = true,
                BeforeVaryingMode = RebarApiCompatibility.ReadVaryingMode(rebar)
            };

            var requestedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (edgeRequested) requestedTypes.Add(RebarHandleType.Edge.ToString());
            if (startRequested) requestedTypes.Add(RebarHandleType.StartOfBar.ToString());
            if (endRequested) requestedTypes.Add(RebarHandleType.EndOfBar.ToString());

            result.BeforeUnretargetedConstraintSignature =
                BuildUnretargetedConstraintSignature(before, requestedTypes);

            Reference? edgeRef = null;
            Reference? startRef = null;
            Reference? endRef = null;
            RebarHostData? hostData = null;
            try
            {
                hostData = RebarHostData.GetRebarHostData(host);
                if (hostData == null)
                    return Failure(request, "当前宿主没有 RebarHostData。", beforeReport);

                if (edgeRequested)
                    edgeRef = ValidateTargetFace(doc, hostData, host.Id, request.EdgeFaceStableReference, "Edge", request);
                if (startRequested)
                    startRef = ValidateTargetFace(doc, hostData, host.Id, request.StartFaceStableReference, "Start", request);
                if (endRequested)
                    endRef = ValidateTargetFace(doc, hostData, host.Id, request.EndFaceStableReference, "End", request);

                if (edgeRequested)
                    EnsureTargetActuallyChanges(before, RebarHandleType.Edge, request.EdgeFaceStableReference, "Edge");
                if (startRequested)
                    EnsureTargetActuallyChanges(before, RebarHandleType.StartOfBar, request.StartFaceStableReference, "Start");
                if (endRequested)
                    EnsureTargetActuallyChanges(before, RebarHandleType.EndOfBar, request.EndFaceStableReference, "End");

                // Candidate preflight is required even for dryRun. It proves that the existing handle can
                // actually target the requested host face as ToCover before any Transaction starts.
                if (edgeRequested) EnsureToCoverCandidate(rebar, RebarHandleType.Edge, edgeRef!, "Edge");
                if (startRequested) EnsureToCoverCandidate(rebar, RebarHandleType.StartOfBar, startRef!, "Start");
                if (endRequested) EnsureToCoverCandidate(rebar, RebarHandleType.EndOfBar, endRef!, "End");

                if (request.DryRun)
                {
                    result.AfterVaryingMode = result.BeforeVaryingMode;
                    result.VaryingModePreserved = true;
                    result.UnretargetedConstraintsPreserved = true;
                    result.AfterUnretargetedConstraintSignature = result.BeforeUnretargetedConstraintSignature;
                    result.Message = "R05 dryRun：目标面和 ToCover candidate 已验证；未启动 Transaction。";
                    return result;
                }

                Transaction? transaction = null;
                try
                {
                    transaction = new Transaction(doc, "YangTools R05 Shape Driven 约束重绑");
                    TransactionStatus startStatus = transaction.Start();
                    if (startStatus != TransactionStatus.Started)
                        return WithError(result, $"无法启动 Revit 事务：{startStatus}");

                    if (edgeRequested)
                        ApplyToCover(rebar, RebarHandleType.Edge, edgeRef!, "Edge");
                    if (startRequested)
                        ApplyToCover(rebar, RebarHandleType.StartOfBar, startRef!, "Start");
                    if (endRequested)
                        ApplyToCover(rebar, RebarHandleType.EndOfBar, endRef!, "End");

                    doc.Regenerate();

                    RebarInspectionReport midReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                    RebarInspectionItem? mid = midReport.Items.FirstOrDefault();
                    if (mid == null || !string.IsNullOrWhiteSpace(mid.Error))
                        throw new InvalidOperationException("重绑后、提交前无法读取 Rebar 快照。");

                    result.SameElementId = mid.ElementId == request.RebarId;
                    result.SameUniqueId = string.Equals(mid.UniqueId, originalUniqueId, StringComparison.Ordinal);
                    result.SameHost = mid.HostId == before.HostId &&
                        string.Equals(mid.HostUniqueId, before.HostUniqueId, StringComparison.Ordinal);
                    if (!result.SameElementId || !result.SameUniqueId || !result.SameHost)
                        throw new InvalidOperationException("R05 重绑破坏了 ElementId / UniqueId / Host 不变量；事务回滚。");

                    result.AfterVaryingMode = RebarApiCompatibility.ReadVaryingMode(rebar);
                    result.VaryingModePreserved = string.Equals(
                        NormalizeVarying(result.BeforeVaryingMode),
                        NormalizeVarying(result.AfterVaryingMode),
                        StringComparison.OrdinalIgnoreCase);
                    if (!result.VaryingModePreserved)
                        throw new InvalidOperationException("R05 重绑改变了 Varying 模式；事务回滚。");

                    result.EdgeApplied = !edgeRequested || VerifyExactFace(mid, RebarHandleType.Edge, request.EdgeFaceStableReference);
                    result.StartApplied = !startRequested || VerifyExactFace(mid, RebarHandleType.StartOfBar, request.StartFaceStableReference);
                    result.EndApplied = !endRequested || VerifyExactFace(mid, RebarHandleType.EndOfBar, request.EndFaceStableReference);
                    if (!result.EdgeApplied || !result.StartApplied || !result.EndApplied)
                        throw new InvalidOperationException("R05 未能把全部请求 handle 精确重绑到指定 ToCover face；事务回滚。");

                    result.AfterUnretargetedConstraintSignature =
                        BuildUnretargetedConstraintSignature(mid, requestedTypes);
                    result.UnretargetedConstraintsPreserved = string.Equals(
                        result.BeforeUnretargetedConstraintSignature,
                        result.AfterUnretargetedConstraintSignature,
                        StringComparison.Ordinal);
                    if (request.PreserveUnretargetedConstraints && !result.UnretargetedConstraintsPreserved)
                        throw new InvalidOperationException("R05 改变了未请求重绑的既有约束；事务回滚。");

                    TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                        transaction,
                        "钢筋约束重绑",
                        "R05 Shape Driven 约束已原位重绑。",
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
                    result.AfterInspection = afterReport;
                    result.ChangedModel = true;
                    result.Message = "R05 Shape Driven Constraint 已原位重绑；身份、宿主、Varying 与未重绑约束已完成 readback。";
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
            catch (Exception ex)
            {
                return WithError(result, ex.Message);
            }
            finally
            {
                try { edgeRef?.Dispose(); } catch { }
                try { startRef?.Dispose(); } catch { }
                try { endRef?.Dispose(); } catch { }
                try { hostData?.Dispose(); } catch { }
            }
        }

        private static Reference ValidateTargetFace(
            Document doc,
            RebarHostData hostData,
            ElementId hostId,
            string stable,
            string label,
            ShapeDrivenConstraintRebindRequest request)
        {
            Reference reference;
            try { reference = Reference.ParseFromStableRepresentation(doc, stable.Trim()); }
            catch (Exception ex) { throw new InvalidOperationException($"无法解析 {label} FaceStableReference：{ex.Message}"); }

            if (reference.ElementId != hostId)
            {
                reference.Dispose();
                throw new InvalidOperationException($"{label} FaceStableReference 不属于当前 Rebar Host {hostId.GetIdValue()}。");
            }

            if (request.RequirePlanarFace)
            {
                GeometryObject? geometry = doc.GetElement(hostId)?.GetGeometryObjectFromReference(reference);
                if (geometry is not PlanarFace)
                {
                    reference.Dispose();
                    throw new InvalidOperationException($"{label} target 不是 PlanarFace；R05 暂不处理曲面。");
                }
            }

            if (request.RequireExposedFace && !hostData.IsFaceExposed(reference))
            {
                reference.Dispose();
                throw new InvalidOperationException($"{label} target 不是 exposed rebar-cover face。");
            }

            if (hostData.GetCoverType(reference) == null)
            {
                reference.Dispose();
                throw new InvalidOperationException($"{label} target 没有可读取的 Rebar Cover Type。");
            }

            return reference;
        }

        private static void EnsureTargetActuallyChanges(
            RebarInspectionItem before,
            RebarHandleType handleType,
            string requestedStable,
            string label)
        {
            RebarConstraintInspection? current = before.Constraints.FirstOrDefault(
                c => string.Equals(c.HandleType, handleType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (current == null) return;

            if (current.TargetHostFaceStableReferences.Any(
                stable => string.Equals(stable, requestedStable.Trim(), StringComparison.Ordinal)))
                throw new InvalidOperationException($"{label} 当前已经约束到该目标面；R05 拒绝无实际变化的重绑。");
        }

        private static void EnsureToCoverCandidate(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            RebarHandleType handleType,
            Reference faceReference,
            string label)
        {
            using (RebarConstraintsManager manager = rebar.GetRebarConstraintsManager())
            {
                using (RebarConstrainedHandle handle = GetUniqueHandle(manager, handleType, label))
                {
                    IList<RebarConstraint> candidates = manager.GetConstraintCandidatesForHandle(handle, faceReference);
                    try
                    {
                        if (!candidates.Any(candidate =>
                        {
                            try { return candidate.GetConstraintType() == RebarConstraintType.ToCover; }
                            catch { return false; }
                        }))
                            throw new InvalidOperationException($"{label} handle 对指定面没有 ToCover candidate。");
                    }
                    finally
                    {
                        foreach (RebarConstraint candidate in candidates)
                            try { candidate.Dispose(); } catch { }
                    }
                }
            }
        }

        private static void ApplyToCover(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            RebarHandleType handleType,
            Reference faceReference,
            string label)
        {
            using (RebarConstraintsManager manager = rebar.GetRebarConstraintsManager())
            using (RebarConstrainedHandle handle = GetUniqueHandle(manager, handleType, label))
            {
                IList<RebarConstraint> candidates = manager.GetConstraintCandidatesForHandle(handle, faceReference);
                try
                {
                    RebarConstraint? target = candidates.FirstOrDefault(candidate =>
                    {
                        try { return candidate.GetConstraintType() == RebarConstraintType.ToCover; }
                        catch { return false; }
                    });
                    if (target == null)
                        throw new InvalidOperationException($"{label} handle 对指定面没有 ToCover candidate。");
                    RebarApiCompatibility.SetPreferredConstraint(manager, handle, target);
                }
                finally
                {
                    foreach (RebarConstraint candidate in candidates)
                        try { candidate.Dispose(); } catch { }
                }
            }
        }

        private static RebarConstrainedHandle GetUniqueHandle(
            RebarConstraintsManager manager,
            RebarHandleType handleType,
            string label)
        {
            IList<RebarConstrainedHandle> handles = manager.GetAllHandles();
            var matches = new List<RebarConstrainedHandle>();
            foreach (RebarConstrainedHandle handle in handles)
            {
                bool match = false;
                try { match = handle.GetHandleType() == handleType; } catch { }
                if (match) matches.Add(handle);
                else try { handle.Dispose(); } catch { }
            }

            if (matches.Count != 1)
            {
                foreach (RebarConstrainedHandle handle in matches)
                    try { handle.Dispose(); } catch { }
                throw new InvalidOperationException(
                    $"{label} 需要唯一 {handleType} handle，当前匹配数量={matches.Count}；R05 拒绝猜测。");
            }
            return matches[0];
        }

        private static bool VerifyExactFace(
            RebarInspectionItem item,
            RebarHandleType handleType,
            string requestedStable)
        {
            RebarConstraintInspection? detail = item.Constraints.FirstOrDefault(
                c => string.Equals(c.HandleType, handleType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (detail == null) return false;
            if (!string.Equals(detail.ConstraintType, RebarConstraintType.ToCover.ToString(), StringComparison.OrdinalIgnoreCase))
                return false;
            return detail.TargetHostFaceStableReferences.Any(
                stable => string.Equals(stable, requestedStable.Trim(), StringComparison.Ordinal));
        }

        private static string BuildUnretargetedConstraintSignature(
            RebarInspectionItem item,
            HashSet<string> retargetedHandleTypes)
        {
            return string.Join(" || ", item.Constraints
                .Where(c => !retargetedHandleTypes.Contains(c.HandleType))
                .Select(c =>
                    $"{c.HandleType}|{c.ConstraintType}|{c.TargetSummary}|" +
                    string.Join(",", c.TargetHostFaceStableReferences.OrderBy(x => x, StringComparer.Ordinal)))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string NormalizeVarying(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return string.Empty;
            if (mode.IndexOf("Vary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mode.IndexOf("Constraint", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Varying";
            return "Uniform";
        }

        private static ShapeDrivenConstraintRebindResult Failure(
            ShapeDrivenConstraintRebindRequest request,
            string error,
            RebarInspectionReport? before = null)
        {
            return new ShapeDrivenConstraintRebindResult
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

        private static ShapeDrivenConstraintRebindResult WithError(
            ShapeDrivenConstraintRebindResult result,
            string error)
        {
            result.Success = false;
            result.ChangedModel = false;
            result.Error = error;
            return result;
        }
    }
}
