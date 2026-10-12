using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    public static class BooleanGeometryEngine
    {
        public static List<Solid> GetLocalSolids(List<FamilyInstance> instances, Transform mainInv)
        {
            List<Solid> result = new List<Solid>();
            foreach (var inst in instances)
            {
                var geom = inst.get_Geometry(new Options { ComputeReferences = true });
                if (geom == null)
                    throw new InvalidOperationException($"目标实例 {inst.Id} 无法读取几何，禁止静默跳过。");

                var solids = new List<Solid>();
                ExtractSolids(geom, solids);
                if (solids.Count == 0)
                    throw new InvalidOperationException($"目标实例 {inst.Id} 没有有效的正体积 Solid，不能参与布尔操作。");

                foreach (var solid in solids)
                {
                    Solid local = SolidUtils.CreateTransformed(solid, mainInv);
                    if (local == null || local.Volume <= 0)
                        throw new InvalidOperationException($"目标实例 {inst.Id} 的几何坐标转换失败。");
                    result.Add(local);
                }
            }
            return result;
        }

        public static void ExtractSolids(GeometryElement geomElem, List<Solid> solids)
        {
            foreach (var geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    if (solid.Volume > 0) solids.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    ExtractSolids(geomInst.GetInstanceGeometry(), solids);
                }
            }
        }

        private static bool HasPositiveIntersection(Solid first, Solid second, out string error)
        {
            error = string.Empty;
            try
            {
                Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                    first,
                    second,
                    BooleanOperationsType.Intersect);
                return intersection != null && intersection.Volume > 1e-9;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                // When Revit cannot even evaluate the intersection, the relationship is
                // ambiguous. Treat it as unsafe instead of emitting possibly overlapping solids.
                return true;
            }
        }

        public static List<Solid> ExecuteBooleanOperations(
            Document famDoc,
            List<Solid> cutSolids,
            List<Solid> unionSolids,
            List<Solid> joinSolids)
        {
            var forms = new FilteredElementCollector(famDoc).OfClass(typeof(GenericForm)).Cast<GenericForm>().ToList();

            List<Solid> mergeableSolids = new List<Solid>();
            foreach (var form in forms)
            {
                if (!form.IsSolid)
                    throw new InvalidOperationException($"族几何 {form.Id} 是 Void；当前算法无法无损保留空心剪切关系，拒绝重建原族。");
                var geom = form.get_Geometry(new Options { ComputeReferences = true });
                if (geom == null)
                    throw new InvalidOperationException($"族几何 {form.Id} 无法读取，不能在重建时删除原形体。");

                var solids = new List<Solid>();
                ExtractSolids(geom, solids);
                if (solids.Count == 0)
                    throw new InvalidOperationException($"族几何 {form.Id} 没有可转换的正体积实体；可能是 Void 或不受支持的形体，已阻止无损性不明的重建。");
                mergeableSolids.AddRange(solids);
            }

            if (mergeableSolids.Count == 0)
                throw new InvalidOperationException("无法获取主体实例的内部实体几何。");

            List<Solid> independentSolids = new List<Solid>();

            foreach (var unionS in unionSolids)
            {
                Solid current = unionS;
                List<Solid> nextMergeable = new List<Solid>();
                foreach (var m in mergeableSolids)
                {
                    // 仅对真实相交实体执行融合；不相交者保留为独立 Solid，避免错误组合。
                    bool intersects = HasPositiveIntersection(current, m, out string intersectionError);
                    if (!string.IsNullOrWhiteSpace(intersectionError))
                        throw new InvalidOperationException("无法判断融合实体是否相交，操作已中止：" + intersectionError);
                    if (!intersects)
                    {
                        nextMergeable.Add(m);
                        continue;
                    }
                    try
                    {
                        current = BooleanOperationsUtils.ExecuteBooleanOperation(current, m, BooleanOperationsType.Union);
                        if (current == null || current.Volume <= 0)
                            throw new InvalidOperationException("融合返回了空实体。");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "检测到相交实体，但融合运算失败。为避免输出重叠实体，本次布尔操作已取消。"
                            + (string.IsNullOrWhiteSpace(intersectionError) ? string.Empty : $" 交集检查：{intersectionError}"),
                            ex);
                    }
                }
                nextMergeable.Add(current);
                mergeableSolids = nextMergeable;
            }

            foreach (var cutS in cutSolids)
            {
                List<Solid> nextMergeable = new List<Solid>();
                foreach (var m in mergeableSolids)
                {
                    // 无交集是合法的 no-op；交集查询失败则 HasPositiveIntersection 返回 true 并要求后续 Difference 必须成功。
                    bool intersects = HasPositiveIntersection(m, cutS, out string intersectionError);
                    if (!string.IsNullOrWhiteSpace(intersectionError))
                        throw new InvalidOperationException("无法判断剪切实体是否相交，操作已中止：" + intersectionError);
                    if (!intersects)
                    {
                        nextMergeable.Add(m);
                        continue;
                    }
                    try
                    {
                        Solid diff = BooleanOperationsUtils.ExecuteBooleanOperation(m, cutS, BooleanOperationsType.Difference);
                        if (diff == null)
                            throw new InvalidOperationException("剪切返回空引用，无法确认是否完整切除。");
                        if (diff.Volume > 0)
                            nextMergeable.Add(diff);
                        // 完全被切除的实体不再加入；若全部被切除，稍后拒绝创建空族。
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "剪切运算失败，不能用未剪切的原实体冒充成功结果。"
                            + (string.IsNullOrWhiteSpace(intersectionError) ? string.Empty : $" 交集检查：{intersectionError}"),
                            ex);
                    }
                }
                mergeableSolids = nextMergeable;

                // 剪切体只作为运算工具，不能作为最终实体重新加入。
                // 旧逻辑会在未检测到交集时凭空输出一份剪切体。
            }

            independentSolids.AddRange(joinSolids);

            var result = new List<Solid>();
            foreach (var m in mergeableSolids)
            {
                if (m.Volume > 0) result.Add(m);
            }
            foreach (var ind in independentSolids)
            {
                if (ind.Volume > 0) result.Add(ind);
            }

            if (result.Count == 0)
                throw new InvalidOperationException("本次布尔操作会产生空的主体族，不允许删除全部原几何。");

            // 准备好所有结果后才删除旧形体；任何删除异常必须让族事务回滚。
            foreach (var form in forms)
            {
                ICollection<ElementId> deleted = famDoc.Delete(form.Id);
                if (deleted == null || !deleted.Contains(form.Id) || famDoc.GetElement(form.Id) != null)
                    throw new InvalidOperationException($"原族几何 {form.Id} 删除未确认，已取消转换。");
            }
            return result;
        }
    }
}
