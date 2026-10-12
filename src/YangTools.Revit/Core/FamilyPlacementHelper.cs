using System;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 在指定标高下创建族实例，同时保证族局部原点最终落在指定的项目世界坐标。
    /// 目标标高只负责实例的标高关联；原几何的世界位置必须保持不变。
    /// </summary>
    internal static class FamilyPlacementHelper
    {
        // 约 0.0003 mm，远小于 Revit 常规建模容差。
        private const double PositionToleranceFeet = 1e-6;

        // 线性布置 P0 硬门禁：实例真实 LocationPoint.Z 与理论世界 Z 的误差不得超过 1 mm。
        private const double ElevationToleranceFeet = 1.0 / 304.8;

        public static FamilyInstance CreateAtWorldPoint(
            Document document,
            FamilySymbol symbol,
            Level targetLevel,
            XYZ worldPoint,
            string operationName)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (targetLevel == null) throw new ArgumentNullException(nameof(targetLevel));
            if (worldPoint == null) throw new ArgumentNullException(nameof(worldPoint));

            FamilyInstance instance = document.Create.NewFamilyInstance(
                worldPoint,
                symbol,
                targetLevel,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural)
                ?? throw new InvalidOperationException($"{operationName}未能创建族实例。");

            document.Regenerate();
            VerifyLevelAssociation(instance, targetLevel, operationName);
            CorrectAndVerifyWorldPoint(document, instance, worldPoint, operationName);
            VerifyLevelAssociation(instance, targetLevel, operationName);
            return instance;
        }


        /// <summary>
        /// 为线性布置创建“单标高 + 相对高程”族实例。
        /// Revit 对 level-based 元素的 Z 方向受标高约束，不能依赖 MoveElement 修正高度；
        /// 必须显式写实例的 Elevation from Level / Offset 参数。
        /// </summary>
        public static FamilyInstance CreateAtLevelRelativeElevation(
            Document document,
            FamilySymbol symbol,
            Level targetLevel,
            XYZ expectedWorldPoint,
            double relativeElevationFeet,
            string operationName)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (targetLevel == null) throw new ArgumentNullException(nameof(targetLevel));
            if (expectedWorldPoint == null) throw new ArgumentNullException(nameof(expectedWorldPoint));

            FamilyPlacementType placementType = symbol.Family?.FamilyPlacementType ?? FamilyPlacementType.Invalid;
            if (placementType == FamilyPlacementType.TwoLevelsBased || placementType == FamilyPlacementType.CurveDrivenStructural)
            {
                throw new InvalidOperationException(
                    $"{operationName}当前不支持族放置类型 {placementType}。" +
                    "两标高结构柱/结构线性构件请使用专用的“沿线布柱”等工具。");
            }

            // 对 level-based 重载，先在所选标高的零偏移位置创建；Z 高度随后由实例偏移参数控制。
            // 不把 expectedWorldPoint.Z 再直接塞给创建重载，避免 Revit 把世界 Z 与标高基准重复解释。
            XYZ seedPoint = new XYZ(expectedWorldPoint.X, expectedWorldPoint.Y, 0.0);
            FamilyInstance instance = document.Create.NewFamilyInstance(
                seedPoint,
                symbol,
                targetLevel,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural)
                ?? throw new InvalidOperationException($"{operationName}未能创建族实例。");

            document.Regenerate();
            VerifyLevelAssociation(instance, targetLevel, operationName);

            BuiltInParameter offsetParameter = SetLevelRelativeOffset(
                document,
                instance,
                relativeElevationFeet,
                targetLevel,
                expectedWorldPoint,
                operationName);

            CorrectAndVerifyHorizontalPoint(document, instance, expectedWorldPoint, operationName);
            VerifyLevelAssociation(instance, targetLevel, operationName);
            VerifyActualWorldElevation(
                instance,
                targetLevel,
                expectedWorldPoint,
                offsetParameter,
                operationName,
                placementType);

            return instance;
        }

        private static BuiltInParameter SetLevelRelativeOffset(
            Document document,
            FamilyInstance instance,
            double relativeElevationFeet,
            Level targetLevel,
            XYZ expectedWorldPoint,
            string operationName)
        {
            BuiltInParameter[] candidates =
            {
                BuiltInParameter.INSTANCE_ELEVATION_PARAM,
                BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM,
                BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,
            };

            var failures = new System.Collections.Generic.List<string>();

            foreach (BuiltInParameter bip in candidates)
            {
                Parameter parameter = instance.get_Parameter(bip);
                if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
                {
                    failures.Add($"{bip}: 参数不存在、只读或不是 Double");
                    continue;
                }

                double originalOffset = parameter.AsDouble();
                bool valueWasSet = false;

                try
                {
                    if (!parameter.Set(relativeElevationFeet))
                    {
                        failures.Add($"{bip}: Parameter.Set 返回 false");
                        continue;
                    }

                    valueWasSet = true;
                    document.Regenerate();

                    if (!(instance.Location is LocationPoint location))
                        throw new InvalidOperationException("生成实例没有可校验的 LocationPoint。");

                    double actualWorldZ = location.Point.Z;
                    double zError = Math.Abs(actualWorldZ - expectedWorldPoint.Z);
                    if (zError <= ElevationToleranceFeet)
                        return bip;

                    failures.Add(
                        $"{bip}: 写入后真实 LocationPoint.Z 未达到理论世界 Z；" +
                        $"实际={actualWorldZ:G8} ft，理论={expectedWorldPoint.Z:G8} ft，" +
                        $"误差={zError * 304.8:G6} mm");
                }
                catch (Exception ex)
                {
                    failures.Add($"{bip}: {ex.Message}");
                }

                if (valueWasSet)
                    RestoreOffsetOrThrow(document, parameter, originalOffset, bip, operationName);
            }

            throw new InvalidOperationException(
                $"{operationName}无法设置族实例的标高相对高程。" +
                "该族没有真正控制实例世界 Z 的可写“Elevation from Level / Offset from Host / Base Offset”参数，" +
                "不适合使用通用线性布置。" +
                $" 目标标高={targetLevel.Name}，相对高程={relativeElevationFeet:G8} ft。" +
                " 尝试结果：" + string.Join("；", failures));
        }

        private static void RestoreOffsetOrThrow(
            Document document,
            Parameter parameter,
            double originalOffset,
            BuiltInParameter bip,
            string operationName)
        {
            try
            {
                if (!parameter.Set(originalOffset))
                    throw new InvalidOperationException("Parameter.Set 返回 false");

                document.Regenerate();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"{operationName}尝试偏移参数 {bip} 后未通过真实 Z 校验，且无法恢复原参数值。" +
                    "为避免叠加错误偏移，本次操作必须回滚。",
                    ex);
            }
        }

        private static void VerifyActualWorldElevation(
            FamilyInstance instance,
            Level targetLevel,
            XYZ expectedWorldPoint,
            BuiltInParameter offsetParameter,
            string operationName,
            FamilyPlacementType placementType)
        {
            if (!(instance.Location is LocationPoint location))
                throw new InvalidOperationException($"{operationName}生成的实例没有可校验的点位置。");

            Parameter parameter = instance.get_Parameter(offsetParameter)
                ?? throw new InvalidOperationException($"{operationName}生成后无法重新读取偏移参数 {offsetParameter}。");

            double actualOffset = parameter.AsDouble();
            double theoreticalWorldZ = targetLevel.ProjectElevation + actualOffset;
            double actualWorldZ = location.Point.Z;
            double zErrorFeet = Math.Abs(actualWorldZ - expectedWorldPoint.Z);
            double zErrorMm = zErrorFeet * 304.8;

            DiagnosticLog.WriteInfo(
                operationName,
                $"线性标高放置 P0 校验；族放置类型={placementType}；" +
                $"LevelId={targetLevel.Id}；目标标高={targetLevel.Name}；" +
                $"Level.Elevation={targetLevel.Elevation:G8} ft；" +
                $"Level.ProjectElevation={targetLevel.ProjectElevation:G8} ft；" +
                $"BuiltInParameter={offsetParameter}；Offset.AsDouble()={actualOffset:G8} ft；" +
                $"理论WorldZ={expectedWorldPoint.Z:G8} ft；" +
                $"参数推导WorldZ={theoreticalWorldZ:G8} ft；" +
                $"LocationPoint.Z={actualWorldZ:G8} ft；误差={zErrorMm:G6} mm");

            if (zErrorFeet > ElevationToleranceFeet)
            {
                throw new InvalidOperationException(
                    $"{operationName}生成实例的世界 Z 校验失败。" +
                    $"理论 LocationPoint.Z={expectedWorldPoint.Z:G8} ft，" +
                    $"实际 LocationPoint.Z={actualWorldZ:G8} ft，误差={zErrorMm:G6} mm，" +
                    "超过 1 mm 硬门禁；本次操作必须回滚。");
            }
        }

        private static void CorrectAndVerifyHorizontalPoint(
            Document document,
            FamilyInstance instance,
            XYZ expectedWorldPoint,
            string operationName)
        {
            if (!(instance.Location is LocationPoint location))
                throw new InvalidOperationException($"{operationName}生成的实例没有可校验的点位置。");

            XYZ actual = location.Point;
            XYZ xyCorrection = new XYZ(expectedWorldPoint.X - actual.X, expectedWorldPoint.Y - actual.Y, 0.0);
            if (xyCorrection.GetLength() > PositionToleranceFeet)
            {
                ElementTransformUtils.MoveElement(document, instance.Id, xyCorrection);
                document.Regenerate();
            }

            if (!(instance.Location is LocationPoint finalLocation))
                throw new InvalidOperationException($"{operationName}生成后无法重新读取实例位置。");

            XYZ finalPoint = finalLocation.Point;
            double xyError = Math.Sqrt(
                Math.Pow(finalPoint.X - expectedWorldPoint.X, 2) +
                Math.Pow(finalPoint.Y - expectedWorldPoint.Y, 2));
            if (xyError > PositionToleranceFeet)
            {
                throw new InvalidOperationException(
                    $"{operationName}生成实例的平面坐标校验失败。" +
                    $"期望XY=({expectedWorldPoint.X:G8},{expectedWorldPoint.Y:G8})，" +
                    $"实际XY=({finalPoint.X:G8},{finalPoint.Y:G8})，误差={xyError:G8} ft。");
            }
        }

        private static void VerifyLevelAssociation(
            FamilyInstance instance,
            Level targetLevel,
            string operationName)
        {
            ElementId actualLevelId = instance.LevelId;
            if (actualLevelId != targetLevel.Id)
            {
                throw new InvalidOperationException(
                    $"{operationName}生成的实例未关联到所选目标标高。" +
                    $"期望标高 Id={targetLevel.Id}，实际标高 Id={actualLevelId}。");
            }
        }

        private static void CorrectAndVerifyWorldPoint(
            Document document,
            FamilyInstance instance,
            XYZ expectedPoint,
            string operationName)
        {
            if (!(instance.Location is LocationPoint initialLocation))
                throw new InvalidOperationException($"{operationName}生成的实例没有可校验的点位置。");

            XYZ initialPoint = initialLocation.Point;
            XYZ correction = expectedPoint - initialPoint;
            double initialError = correction.GetLength();

            // 某些族模板会根据“标高 + 偏移”重新解释传入 Z。
            // 不猜测具体偏移参数，而是以实例实际位置为准做一次确定性校正。
            if (initialError > PositionToleranceFeet)
            {
                ElementTransformUtils.MoveElement(document, instance.Id, correction);
                document.Regenerate();
            }

            if (!(instance.Location is LocationPoint finalLocation))
                throw new InvalidOperationException($"{operationName}生成后无法重新读取实例位置。");

            XYZ actualPoint = finalLocation.Point;
            double finalError = actualPoint.DistanceTo(expectedPoint);
            if (finalError > PositionToleranceFeet)
            {
                throw new InvalidOperationException(
                    $"{operationName}生成实例的世界坐标校验失败。" +
                    $"期望=({expectedPoint.X:G8},{expectedPoint.Y:G8},{expectedPoint.Z:G8})，" +
                    $"实际=({actualPoint.X:G8},{actualPoint.Y:G8},{actualPoint.Z:G8})，" +
                    $"误差={finalError:G8} ft。");
            }

            DiagnosticLog.WriteInfo(
                operationName,
                $"目标标高={GetTargetLevelName(instance)}；" +
                $"世界坐标期望=({expectedPoint.X:G8},{expectedPoint.Y:G8},{expectedPoint.Z:G8})；" +
                $"初始误差={initialError:G8} ft；最终误差={finalError:G8} ft");
        }

        private static string GetTargetLevelName(FamilyInstance instance)
        {
            try
            {
                Document document = instance.Document;
                ElementId levelId = instance.LevelId;
                return levelId != ElementId.InvalidElementId
                    ? document.GetElement(levelId)?.Name ?? levelId.ToString()
                    : "未报告";
            }
            catch
            {
                return "读取失败";
            }
        }
    }
}
