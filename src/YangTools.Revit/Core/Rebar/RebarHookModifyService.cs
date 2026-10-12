using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// R06 minimal hook modifier for existing Shape Driven Rebar.
    /// Hook compatibility is delegated to native Revit; invalid hook/shape combinations rollback.
    /// Use -1 to remove a hook, null to leave that end unchanged.
    /// </summary>
    public static class RebarHookModifyService
    {
        public static RebarHookModifyResult Execute(UIApplication uiapp, RebarHookModifyRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Fail(request, "当前没有活动 Revit 文档。");
            Document doc = uidoc.Document;
            var rebar = doc.GetElement(ElementIdExtensions.CreateId(request.RebarId)) as Autodesk.Revit.DB.Structure.Rebar;
            if (rebar == null) return Fail(request, $"找不到 Rebar ElementId={request.RebarId}。");
            if (rebar.IsRebarFreeForm()) return Fail(request, "R06 Hook 修改当前仅支持 Shape Driven Rebar。");
            if (!string.IsNullOrWhiteSpace(request.ExpectedUniqueId) && rebar.UniqueId != request.ExpectedUniqueId)
                return Fail(request, "ExpectedUniqueId 与当前 Rebar 不一致。");
            if (!request.StartHookTypeId.HasValue && !request.EndHookTypeId.HasValue)
                return Fail(request, "至少指定 StartHookTypeId 或 EndHookTypeId；使用 -1 表示移除 Hook。");

            RebarInspectionReport beforeReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
            RebarInspectionItem? before = beforeReport.Items.FirstOrDefault();
            if (before == null) return Fail(request, "无法读取修改前 Rebar。", beforeReport);

            var result = new RebarHookModifyResult
            {
                Success = true,
                DryRun = request.DryRun,
                ChangedModel = false,
                RebarId = request.RebarId,
                UniqueId = rebar.UniqueId,
                HostId = before.HostId,
                BeforeStartHookTypeId = before.StartHookTypeId,
                BeforeEndHookTypeId = before.EndHookTypeId,
                AfterStartHookTypeId = before.StartHookTypeId,
                AfterEndHookTypeId = before.EndHookTypeId,
                SameElementId = true,
                SameUniqueId = true,
                SameHost = true,
                BeforeInspection = beforeReport,
                AfterInspection = beforeReport
            };

            ElementId? startId = ResolveHookType(doc, request.StartHookTypeId, "Start");
            ElementId? endId = ResolveHookType(doc, request.EndHookTypeId, "End");

            if (request.StartHookTypeId.HasValue && NormalizeHookId(before.StartHookTypeId) == request.StartHookTypeId.Value &&
                !request.EndHookTypeId.HasValue)
                return Error(result, "Start Hook 已经是请求类型；拒绝无变化修改。");
            if (request.EndHookTypeId.HasValue && NormalizeHookId(before.EndHookTypeId) == request.EndHookTypeId.Value &&
                !request.StartHookTypeId.HasValue)
                return Error(result, "End Hook 已经是请求类型；拒绝无变化修改。");

            if (request.DryRun)
            {
                result.Message = "R06 Hook dryRun：目标 Hook Type 已验证；未启动 Transaction。";
                return result;
            }

            Transaction? tx = null;
            try
            {
                tx = new Transaction(doc, "YangTools R06 Hook 修改");
                TransactionStatus started = tx.Start();
                if (started != TransactionStatus.Started) return Error(result, $"无法启动 Revit 事务：{started}");

                if (startId != null) rebar.SetHookTypeId(0, startId);
                if (endId != null) rebar.SetHookTypeId(1, endId);
                doc.Regenerate();

                RebarInspectionReport midReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                RebarInspectionItem? mid = midReport.Items.FirstOrDefault();
                if (mid == null) throw new InvalidOperationException("Hook 修改后无法读取 Rebar。");

                result.AfterStartHookTypeId = mid.StartHookTypeId;
                result.AfterEndHookTypeId = mid.EndHookTypeId;
                result.SameElementId = mid.ElementId == request.RebarId;
                result.SameUniqueId = mid.UniqueId == rebar.UniqueId;
                result.SameHost = mid.HostId == before.HostId;
                if (!result.SameElementId || !result.SameUniqueId || !result.SameHost)
                    throw new InvalidOperationException("Hook 修改破坏了 ElementId / UniqueId / Host 不变量；事务回滚。");
                if (request.StartHookTypeId.HasValue && NormalizeHookId(mid.StartHookTypeId) != request.StartHookTypeId.Value)
                    throw new InvalidOperationException("Start Hook readback 与请求不一致；事务回滚。");
                if (request.EndHookTypeId.HasValue && NormalizeHookId(mid.EndHookTypeId) != request.EndHookTypeId.Value)
                    throw new InvalidOperationException("End Hook readback 与请求不一致；事务回滚。");

                TransactionStatus committed = tx.Commit();
                result.TransactionStatus = committed.ToString();
                if (committed != TransactionStatus.Committed)
                    throw new InvalidOperationException($"事务提交状态不是 Committed：{committed}");

                result.ChangedModel = true;
                result.AfterInspection = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                result.Message = "R06 Hook 已原位修改并完成 R00.5 readback。";
                return result;
            }
            catch (Exception ex)
            {
                try { if (tx != null && tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); } catch { }
                result.Success = false;
                result.ChangedModel = false;
                result.Error = ex.Message;
                result.TransactionStatus = tx?.GetStatus().ToString() ?? string.Empty;
                result.AfterInspection = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                return result;
            }
            finally { try { tx?.Dispose(); } catch { } }
        }

        private static ElementId? ResolveHookType(Document doc, long? idValue, string label)
        {
            if (!idValue.HasValue) return null;
            if (idValue.Value == -1) return ElementId.InvalidElementId;
            ElementId id = ElementIdExtensions.CreateId(idValue.Value);
            if (doc.GetElement(id) is not RebarHookType)
                throw new InvalidOperationException($"{label}HookTypeId={idValue.Value} 不是 RebarHookType。");
            return id;
        }

        private static long NormalizeHookId(long? value) => value ?? -1;

        private static RebarHookModifyResult Fail(RebarHookModifyRequest request, string error, RebarInspectionReport? before = null)
        {
            return new RebarHookModifyResult { Success = false, DryRun = request.DryRun, ChangedModel = false, RebarId = request.RebarId, Error = error, BeforeInspection = before, AfterInspection = before };
        }
        private static RebarHookModifyResult Error(RebarHookModifyResult result, string error)
        {
            result.Success = false; result.ChangedModel = false; result.Error = error; return result;
        }
    }
}
