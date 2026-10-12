using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace YangTools.Revit.Core
{
    /// <summary>不可变的批量标高修改输入；调用者负责在主线程执行。</summary>
    public sealed class LevelModifierRequest
    {
        public ElementId TargetLevelId { get; private set; }
        public IReadOnlyList<ElementId> ElementIds { get; private set; }

        private LevelModifierRequest(ElementId targetLevelId, ElementId[] elementIds)
        {
            TargetLevelId = targetLevelId;
            ElementIds = Array.AsReadOnly(elementIds);
        }

        public static LevelModifierRequest Create(ElementId targetLevelId, IEnumerable<ElementId> elementIds)
        {
            if (targetLevelId == null || targetLevelId == ElementId.InvalidElementId)
                throw new ArgumentException("目标标高无效。", nameof(targetLevelId));
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            ElementId[] ids = elementIds.ToArray();
            if (ids.Length == 0) throw new ArgumentException("未选择任何图元。", nameof(elementIds));
            if (ids.Any(id => id == null || id == ElementId.InvalidElementId))
                throw new ArgumentException("选择中包含无效图元 ID。", nameof(elementIds));
            if (ids.Distinct().Count() != ids.Length)
                throw new ArgumentException("选择中存在重复图元 ID。", nameof(elementIds));
            return new LevelModifierRequest(targetLevelId, ids);
        }
    }

    public sealed class LevelModifierResult
    {
        public bool Committed { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public bool NoChange { get; internal set; }
        public int ChangedCount { get; internal set; }
        public int UnchangedCount { get; internal set; }
        public string Error { get; internal set; }
        public IReadOnlyDictionary<ElementId, ElementId> EffectiveIds { get; internal set; }
    }

    /// <summary>
    /// 所有目标先预检，再用一笔 Revit Transaction 修改。禁止静默跳过图元和部分提交。
    /// 替换族实例仅限无宿主、非镜像、非结构的一标高点式族。
    /// </summary>
    public static class LevelModifierService
    {
        private const double WorldPointToleranceFeet = 1.0 / 304.8; // 1 mm

        private sealed class ItemPlan
        {
            public ElementId Id;
            public bool Change;
            public XYZ Point;
            public XYZ CurveStart;
            public XYZ CurveEnd;
            public bool HasCurve;
            public bool Recreate;
            public bool Mep;
            public ElementId SymbolId;
            public double Rotation;
        }

        public static LevelModifierResult Execute(Document doc, LevelModifierRequest request)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (request == null) throw new ArgumentNullException(nameof(request));
            Level target = doc.GetElement(request.TargetLevelId) as Level;
            if (target == null) return Failed("目标标高不存在于当前文档。", null);
            if (doc.IsModifiable) return Failed("当前文档已存在修改事务，无法保证单次独立 Undo。", null);

            List<ItemPlan> plans = new List<ItemPlan>();
            try
            {
                foreach (ElementId id in request.ElementIds)
                {
                    Element element = doc.GetElement(id);
                    if (element == null) throw new InvalidOperationException($"图元 {id} 已被删除。整批未执行。");
                    if (element.Pinned || element.GroupId != ElementId.InvalidElementId)
                        throw new InvalidOperationException($"图元 {id} 被固定或属于模型组，整批未执行。");
                    ItemPlan plan = Inspect(doc, element, target);
                    plans.Add(plan);
                }
            }
            catch (Exception ex)
            {
                return Failed("预检失败：" + ex.Message, null);
            }

            List<ItemPlan> changed = plans.Where(p => p.Change).ToList();
            if (changed.Count == 0)
                return new LevelModifierResult
                {
                    NoChange = true, ReadbackConfirmed = true, UnchangedCount = plans.Count,
                    EffectiveIds = new Dictionary<ElementId, ElementId>()
                };

            Dictionary<ElementId, ElementId> actualIds = new Dictionary<ElementId, ElementId>();
            bool committed = false;
            using (Transaction tx = new Transaction(doc, "修改标高保持原位"))
            {
                try
                {
                    if (tx.Start() != TransactionStatus.Started)
                        return Failed("标高修改事务无法启动。", null);
                    foreach (ItemPlan plan in changed)
                    {
                        Element source = doc.GetElement(plan.Id);
                        if (source == null) throw new InvalidOperationException($"图元 {plan.Id} 执行前已不存在。");
                        ElementId effectiveId;
                        if (!LevelModifierEngine.TryChangeLevel(doc, source, target, out effectiveId))
                            throw new InvalidOperationException($"图元 {plan.Id} 无法修改目标标高。");
                        actualIds.Add(plan.Id, effectiveId);
                        doc.Regenerate();
                        VerifyItem(doc, plan, effectiveId, target);
                    }
                    TransactionStatus status = tx.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        string stateMessage = $"事务没有确认提交（{status}），不得当作成功。";
                        if (tx.GetStatus() == TransactionStatus.Started)
                        {
                            try
                            {
                                TransactionStatus rollbackStatus = tx.RollBack();
                                if (rollbackStatus != TransactionStatus.RolledBack)
                                    stateMessage += $" 回滚结果未确认：{rollbackStatus}。";
                            }
                            catch (Exception rollbackEx)
                            {
                                stateMessage += " 回滚状态未知：" + rollbackEx.Message;
                            }
                        }
                        return Failed(stateMessage, actualIds);
                    }
                    committed = true;
                }
                catch (Exception ex)
                {
                    if (committed)
                        return Failed("模型已提交，但后续操作异常：" + ex.Message, actualIds, true);
                    string rollback = "";
                    try
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                        {
                            TransactionStatus state = tx.RollBack();
                            if (state != TransactionStatus.RolledBack)
                                rollback = $"；回滚状态未确认：{state}";
                        }
                        else if (tx.GetStatus() != TransactionStatus.RolledBack)
                            rollback = $"；事务最终状态未确认：{tx.GetStatus()}";
                    }
                    catch (Exception rollbackEx)
                    {
                        rollback = "；回滚无法确认：" + rollbackEx.Message;
                    }
                    return Failed("整批操作已中止：" + ex.Message + rollback, actualIds);
                }
            }

            // Commit 后才做最终回读；异常不再误报为「修改失败、已回滚」。
            try
            {
                foreach (ItemPlan plan in changed)
                    VerifyItem(doc, plan, actualIds[plan.Id], target);
                return new LevelModifierResult
                {
                    Committed = true, ReadbackConfirmed = true,
                    ChangedCount = changed.Count, UnchangedCount = plans.Count - changed.Count,
                    EffectiveIds = actualIds
                };
            }
            catch (Exception ex)
            {
                return Failed("事务已提交，但最终模型回读未确认，请勿直接重复执行：" + ex.Message, actualIds, true);
            }
        }

        private static ItemPlan Inspect(Document doc, Element element, Level target)
        {
            ItemPlan plan = new ItemPlan { Id = element.Id };
            LocationPoint point = element.Location as LocationPoint;
            LocationCurve curve = element.Location as LocationCurve;
            if (point != null) plan.Point = point.Point;
            else if (curve != null && curve.Curve != null && curve.Curve.IsBound)
            {
                plan.HasCurve = true;
                plan.CurveStart = curve.Curve.GetEndPoint(0);
                plan.CurveEnd = curve.Curve.GetEndPoint(1);
            }
            else throw new InvalidOperationException($"图元 {element.Id} 没有可核对的点或曲线位置，不执行可能移位的修改。");

            Parameter startLevel = element.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            Parameter endLevel = element.get_Parameter(BuiltInParameter.RBS_END_LEVEL_PARAM);
            Parameter startOffset = element.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
            Parameter endOffset = element.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);
            if (startLevel != null && endLevel != null && startOffset != null && endOffset != null)
            {
                if (startLevel.IsReadOnly || endLevel.IsReadOnly || startOffset.IsReadOnly || endOffset.IsReadOnly
                    || startLevel.StorageType != StorageType.ElementId || endLevel.StorageType != StorageType.ElementId
                    || startOffset.StorageType != StorageType.Double || endOffset.StorageType != StorageType.Double
                    || !(doc.GetElement(startLevel.AsElementId()) is Level)
                    || !(doc.GetElement(endLevel.AsElementId()) is Level))
                    throw new InvalidOperationException($"图元 {element.Id} 的 MEP 两端标高或偏移无效/只读。");
                plan.Mep = true;
                plan.Change = startLevel.AsElementId() != target.Id || endLevel.AsElementId() != target.Id;
                return plan;
            }

            Parameter level = LevelModifierEngine.GetLevelParameter(element);
            Parameter offset = LevelModifierEngine.GetOffsetParameter(element, level);
            if (level != null && !level.IsReadOnly && level.StorageType == StorageType.ElementId
                && offset != null && !offset.IsReadOnly && offset.StorageType == StorageType.Double
                && doc.GetElement(level.AsElementId()) is Level)
            {
                plan.Change = level.AsElementId() != target.Id;
                return plan;
            }

            FamilyInstance fi = element as FamilyInstance;
            if (fi != null && fi.LevelId == target.Id)
            {
                plan.Change = false;
                return plan;
            }
            if (fi == null || point == null || fi.Symbol == null || fi.Symbol.Family == null)
                throw new InvalidOperationException($"图元 {element.Id} 不支持安全修改标高，缺少可写标高/偏移。");
            if (fi.Host != null || fi.SuperComponent != null || fi.Mirrored || fi.HandFlipped || fi.FacingFlipped
                || fi.StructuralType != StructuralType.NonStructural
                || fi.Symbol.Family.FamilyPlacementType != FamilyPlacementType.OneLevelBased)
                throw new InvalidOperationException($"图元 {element.Id} 为宿主、镜像、结构或非单标高点式族，不允许删除重建以免丢失关系。");
            plan.Change = true;
            plan.Recreate = true;
            plan.SymbolId = fi.Symbol.Id;
            plan.Rotation = point.Rotation;
            return plan;
        }

        private static void VerifyItem(Document doc, ItemPlan plan, ElementId effectiveId, Level target)
        {
            Element actual = doc.GetElement(effectiveId);
            if (actual == null) throw new InvalidOperationException($"图元 {plan.Id} 修改后图元 ID {effectiveId} 不存在。");
            LocationPoint point = actual.Location as LocationPoint;
            LocationCurve curve = actual.Location as LocationCurve;
            if (plan.Point != null && (point == null || point.Point.DistanceTo(plan.Point) > WorldPointToleranceFeet))
                throw new InvalidOperationException($"图元 {plan.Id} 修改后世界位置偏差超过 1mm。");
            if (plan.HasCurve && (curve == null || curve.Curve == null || !curve.Curve.IsBound
                || curve.Curve.GetEndPoint(0).DistanceTo(plan.CurveStart) > WorldPointToleranceFeet
                || curve.Curve.GetEndPoint(1).DistanceTo(plan.CurveEnd) > WorldPointToleranceFeet))
                throw new InvalidOperationException($"图元 {plan.Id} 修改后曲线端点位置发生变化。");
            if (plan.Mep)
            {
                if (actual.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)?.AsElementId() != target.Id
                    || actual.get_Parameter(BuiltInParameter.RBS_END_LEVEL_PARAM)?.AsElementId() != target.Id)
                    throw new InvalidOperationException($"图元 {plan.Id} 两端标高回读不匹配。");
            }
            else if (plan.Recreate)
            {
                if (!(actual is FamilyInstance instance) || instance.LevelId != target.Id)
                    throw new InvalidOperationException($"图元 {plan.Id} 重建后的标高关联不正确。");
                if (instance.Symbol.Id != plan.SymbolId
                    || !(instance.Location is LocationPoint recreatedPoint)
                    || Math.Abs(recreatedPoint.Rotation - plan.Rotation) > 1e-5)
                    throw new InvalidOperationException($"图元 {plan.Id} 重建后族类型或旋转角度未保持。");
            }
            else
            {
                Parameter level = LevelModifierEngine.GetLevelParameter(actual);
                if (level == null || level.AsElementId() != target.Id)
                    throw new InvalidOperationException($"图元 {plan.Id} 标高参数回读不匹配。");
            }
        }

        private static LevelModifierResult Failed(string reason, Dictionary<ElementId, ElementId> ids, bool committed = false)
        {
            return new LevelModifierResult
            {
                Committed = committed, ReadbackConfirmed = false, Error = reason,
                EffectiveIds = ids ?? new Dictionary<ElementId, ElementId>()
            };
        }
    }
}
