using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace YangTools.Revit.Core
{
    public static class LevelModifierEngine
    {
        public static bool TryChangeLevel(Document doc, Element elem, Level targetLevel)
        {
            ElementId ignored;
            return TryChangeLevel(doc, elem, targetLevel, out ignored);
        }

        public static bool TryChangeLevel(Document doc, Element elem, Level targetLevel, out ElementId effectiveId)
        {
            effectiveId = elem.Id;
            // 斜管、斜风管等 MEP 曲线具有独立的起点/终点标高与偏移。
            // 必须同时修改两端，才能保持原有坡度和世界坐标高度。
            if (TryChangeMepStartEndLevels(doc, elem, targetLevel))
                return true;

            Parameter levelParameter = GetLevelParameter(elem);
            if (levelParameter != null && !levelParameter.IsReadOnly)
            {
                Parameter offsetParameter = GetOffsetParameter(elem, levelParameter);
                if (offsetParameter != null && !offsetParameter.IsReadOnly)
                {
                    ElementId levelId = levelParameter.AsElementId();
                    if (levelId == targetLevel.Id)
                        return true;

                    Element originalLevelElem = doc.GetElement(levelId);
                    Level originalLevel = originalLevelElem as Level;
                    if (originalLevel == null)
                        return false;
                    // ProjectElevation 是项目世界坐标，不受标高基准显示方式影响。
                    double originalElevation = originalLevel.ProjectElevation;
                    double originalOffset = offsetParameter.AsDouble();
                    double newOffset = originalElevation + originalOffset - targetLevel.ProjectElevation;

                    if (!levelParameter.Set(targetLevel.Id) || !offsetParameter.Set(newOffset))
                        throw new InvalidOperationException("标高或 Offset 参数写入失败，必须回滚整批。");
                    return true;
                }
            }

            FamilyInstance fi = elem as FamilyInstance;
            if (fi != null)
            {
                // 不能通过复制重建改变宿主、镜像、结构族或组合实例的模型关系。
                if (fi.Host != null || fi.SuperComponent != null || fi.Mirrored || fi.HandFlipped || fi.FacingFlipped
                    || fi.StructuralType != StructuralType.NonStructural
                    || fi.Symbol == null || fi.Symbol.Family == null
                    || fi.Symbol.Family.FamilyPlacementType != FamilyPlacementType.OneLevelBased)
                    return false;
                Location location = fi.Location;
                LocationPoint locationPoint = location as LocationPoint;
                if (locationPoint != null)
                {
                    XYZ point = locationPoint.Point;
                    FamilySymbol symbol = fi.Symbol;
                    if (!symbol.IsActive)
                        symbol.Activate();

                    using (SubTransaction subTransaction = new SubTransaction(doc))
                    {
                        try
                        {
                            if (subTransaction.Start() != TransactionStatus.Started)
                                return false;

                            FamilyInstance newInstance = FamilyPlacementHelper.CreateAtWorldPoint(
                                doc,
                                symbol,
                                targetLevel,
                                point,
                                "标高修改");

                            if (Math.Abs(locationPoint.Rotation) > 0.0001)
                            {
                                Line axis = Line.CreateBound(point, point + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(doc, newInstance.Id, axis, locationPoint.Rotation);
                            }

                            CopyInstanceParameters(fi, newInstance, levelParameter, GetOffsetParameter(elem, levelParameter));
                            doc.Delete(elem.Id);

                            TransactionStatus status = subTransaction.Commit();
                            if (status != TransactionStatus.Committed)
                                throw new InvalidOperationException($"重建子事务提交状态未确认：{status}");
                            effectiveId = newInstance.Id;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (subTransaction.GetStatus() == TransactionStatus.Started
                                    && subTransaction.RollBack() != TransactionStatus.RolledBack)
                                    throw new InvalidOperationException("重建子事务回滚未确认。", ex);
                            }
                            catch (Exception rollbackEx)
                            {
                                throw new InvalidOperationException("族实例重建回滚状态未知。", rollbackEx);
                            }
                            throw new InvalidOperationException("族实例重建失败，需整批回滚。", ex);
                        }
                    }
                }
            }
            return false;
        }


        private static bool TryChangeMepStartEndLevels(Document doc, Element elem, Level targetLevel)
        {
            Parameter startLevel = elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            Parameter endLevel = elem.get_Parameter(BuiltInParameter.RBS_END_LEVEL_PARAM);
            Parameter startOffset = elem.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
            Parameter endOffset = elem.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);

            if (startLevel == null || endLevel == null || startOffset == null || endOffset == null)
                return false;
            if (startLevel.StorageType != StorageType.ElementId || endLevel.StorageType != StorageType.ElementId)
                return false;
            if (startOffset.StorageType != StorageType.Double || endOffset.StorageType != StorageType.Double)
                return false;
            if (startLevel.IsReadOnly || endLevel.IsReadOnly || startOffset.IsReadOnly || endOffset.IsReadOnly)
                return false;

            Level oldStartLevel = doc.GetElement(startLevel.AsElementId()) as Level;
            Level oldEndLevel = doc.GetElement(endLevel.AsElementId()) as Level;
            if (oldStartLevel == null || oldEndLevel == null)
                return false;

            double absoluteStart = oldStartLevel.ProjectElevation + startOffset.AsDouble();
            double absoluteEnd = oldEndLevel.ProjectElevation + endOffset.AsDouble();

            if (!startLevel.Set(targetLevel.Id) || !endLevel.Set(targetLevel.Id)
                || !startOffset.Set(absoluteStart - targetLevel.ProjectElevation)
                || !endOffset.Set(absoluteEnd - targetLevel.ProjectElevation))
                throw new InvalidOperationException("MEP 起点/终点标高或偏移写入失败。");
            return true;
        }

        private static void CopyInstanceParameters(Element source, Element target, Parameter levelParam, Parameter offsetParam)
        {
            foreach (Parameter srcParam in source.Parameters)
            {
                if ((levelParam != null && srcParam.Id == levelParam.Id)
                    || (offsetParam != null && srcParam.Id == offsetParam.Id)
                    || srcParam.IsReadOnly
                    || srcParam.Definition == null)
                    continue;

                Parameter tgtParam = target.get_Parameter(srcParam.Definition);
                if (tgtParam == null || tgtParam.IsReadOnly) continue;

                switch (srcParam.StorageType)
                {
                    case StorageType.Integer:
                        tgtParam.Set(srcParam.AsInteger());
                        break;
                    case StorageType.Double:
                        tgtParam.Set(srcParam.AsDouble());
                        break;
                    case StorageType.String:
                        tgtParam.Set(srcParam.AsString());
                        break;
                    case StorageType.ElementId:
                        tgtParam.Set(srcParam.AsElementId());
                        break;
                }
            }
        }

        public static Parameter GetLevelParameter(Element elem)
        {
            BuiltInParameter[] builtInParams = new BuiltInParameter[]
            {
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM,
                BuiltInParameter.LEVEL_PARAM,
                BuiltInParameter.ROOM_LEVEL_ID,
                BuiltInParameter.RBS_START_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                BuiltInParameter.WALL_BASE_CONSTRAINT
            };

            foreach (var bip in builtInParams)
            {
                Parameter param = elem.get_Parameter(bip);
                if (param != null && param.StorageType == StorageType.ElementId)
                    return param;
            }

            foreach (Parameter param in elem.Parameters)
            {
                if (param.StorageType != StorageType.ElementId) continue;
                string name = param.Definition.Name;
                if (name.Contains("Level") || name.Contains("标高") || name.Contains("Base Constraint")
                    || name.Contains("底部约束") || name.Contains("Reference Level"))
                {
                    ElementId id = param.AsElementId();
                    if (id == ElementId.InvalidElementId) return param;
                    if (elem.Document.GetElement(id) is Level) return param;
                }
            }
            return null;
        }

        public static Parameter GetOffsetParameter(Element elem, Parameter levelParam)
        {
            if (levelParam != null && levelParam.Id.GetIdValue() < 0)
            {
                BuiltInParameter bip = (BuiltInParameter)(int)levelParam.Id.GetIdValue();
                BuiltInParameter? offsetBip = null;
                if (bip == BuiltInParameter.FAMILY_LEVEL_PARAM)
                    offsetBip = BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM;
                else if (bip == BuiltInParameter.WALL_BASE_CONSTRAINT)
                    offsetBip = BuiltInParameter.WALL_BASE_OFFSET;
                else if (bip == BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                    offsetBip = BuiltInParameter.INSTANCE_ELEVATION_PARAM;
                else if (bip == BuiltInParameter.RBS_START_LEVEL_PARAM)
                    offsetBip = BuiltInParameter.RBS_START_OFFSET_PARAM;
                else if (bip == BuiltInParameter.LEVEL_PARAM)
                    offsetBip = BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM;

                if (offsetBip.HasValue)
                {
                    Parameter result = elem.get_Parameter(offsetBip.Value);
                    // 偏移参数必须是 Double；否则后续 AsDouble 会直接抛异常。
                    if (result != null && result.StorageType == StorageType.Double) return result;
                }
            }

            string[] names = new string[] { "Offset", "偏移", "Base Offset", "底部偏移", "Elevation from Level", "相对标高", "标高偏移", "偏移量" };
            foreach (string name in names)
            {
                Parameter param = elem.LookupParameter(name);
                if (param != null && param.StorageType == StorageType.Double)
                    return param;
            }
            return null;
        }
    }
}
