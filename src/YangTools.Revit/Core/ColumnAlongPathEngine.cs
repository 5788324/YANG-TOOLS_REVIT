using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Models;

namespace YangTools.Revit.Core
{
    public sealed class ColumnAlongPathEngine
    {
        private const double ChainToleranceFeet = 0.005; // about 1.5 mm
        private const double PointToleranceFeet = 1e-6;

        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public ColumnAlongPathEngine(UIDocument uidoc)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
        }

        public List<Curve>? PickContinuousModelLines()
        {
            IList<Reference> references = _uidoc.Selection.PickObjects(
                ObjectType.Element,
                new ModelLineSelectionFilter(),
                "请选择首尾连续的模型线，完成后点击“完成”");

            if (references == null || references.Count == 0)
                return null;

            var curves = new List<Curve>();
            foreach (Reference reference in references)
            {
                if (!(_doc.GetElement(reference.ElementId) is ModelCurve modelCurve))
                    continue;

                Curve curve = modelCurve.GeometryCurve;
                if (curve != null && curve.Length > PointToleranceFeet)
                {
                    Curve copiedCurve = curve.CreateTransformed(Transform.Identity);
                    if (!IsApproximatelyHorizontal(copiedCurve))
                    {
                        throw new InvalidOperationException(
                            $"模型线 ID {modelCurve.Id.GetIdValue()} 不是水平路线。沿线布柱会由 Base/Top Level 控制竖向约束，请选择平面内的模型线。");
                    }
                    curves.Add(copiedCurve);
                }
            }

            if (curves.Count == 0)
                return null;

            return BuildOpenContinuousChain(curves, curves[0]);
        }

        public ColumnAlongPathPlacementResult ExecutePlacement(ColumnAlongPathPlacementRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ChainedCurves == null || request.ChainedCurves.Count == 0)
                throw new InvalidOperationException("请先选择连续模型线路线。");
            if (_doc.IsReadOnly)
                throw new InvalidOperationException("当前项目为只读状态，不能沿线布置柱。");
            ValidateFinite(request.SpacingMm, "柱间距");
            ValidateFinite(request.StartOffsetMm, "首柱距路线起点");
            ValidateFinite(request.BaseOffsetMm, "Base Offset");
            ValidateFinite(request.TopOffsetMm, "Top Offset");
            ValidateFinite(request.RotationOffsetDegrees, "旋转偏移角度");
            ValidateFinite(request.RouteStartMileageM, "路线起点里程");
            if (request.SpacingMm <= 0)
                throw new InvalidOperationException("柱间距必须大于 0 mm。");
            if (request.StartOffsetMm < 0)
                throw new InvalidOperationException("首柱距路线起点不能小于 0 mm。");

            FamilySymbol? symbol = _doc.GetElement(
                ElementIdExtensions.CreateId(request.ColumnTypeId)) as FamilySymbol;
            Level? baseLevel = _doc.GetElement(
                ElementIdExtensions.CreateId(request.BaseLevelId)) as Level;
            Level? topLevel = _doc.GetElement(
                ElementIdExtensions.CreateId(request.TopLevelId)) as Level;

            if (symbol == null || !symbol.IsValidObject)
                throw new InvalidOperationException("所选柱类型已经失效，请重新打开窗口并选择柱类型。");
            if (baseLevel == null || topLevel == null)
                throw new InvalidOperationException("所选标高已经失效，请重新打开窗口并选择标高。");

            long categoryId = symbol.Category?.Id.GetIdValue() ?? 0;
            bool isStructuralColumn = categoryId == (long)BuiltInCategory.OST_StructuralColumns;
            bool isArchitecturalColumn = categoryId == (long)BuiltInCategory.OST_Columns;
            if (!isStructuralColumn && !isArchitecturalColumn)
                throw new InvalidOperationException("所选族类型不是结构柱或建筑柱。");

            FamilyPlacementType placementType = symbol.Family.FamilyPlacementType;
            if (placementType != FamilyPlacementType.OneLevelBased
                && placementType != FamilyPlacementType.TwoLevelsBased)
            {
                throw new InvalidOperationException(
                    $"所选柱族的放置方式为 {placementType}，当前功能只支持标高型柱族（OneLevelBased / TwoLevelsBased）。");
            }

            double baseOffsetFeet = request.BaseOffsetMm / 304.8;
            double topOffsetFeet = request.TopOffsetMm / 304.8;
            double baseElevation = baseLevel.ProjectElevation + baseOffsetFeet;
            double topElevation = topLevel.ProjectElevation + topOffsetFeet;
            if (topElevation <= baseElevation + PointToleranceFeet)
            {
                throw new InvalidOperationException(
                    "柱顶高程必须高于柱底高程。请检查 Base Level、Base Offset、Top Level 和 Top Offset。");
            }

            List<Curve> effectiveCurves = request.ReversePath
                ? ReverseChain(request.ChainedCurves)
                : request.ChainedCurves.Select(c => c.CreateTransformed(Transform.Identity)).ToList();

            double totalLength = effectiveCurves.Sum(c => c.Length);
            double spacingFeet = request.SpacingMm / 304.8;
            double startOffsetFeet = request.StartOffsetMm / 304.8;
            if (startOffsetFeet > totalLength + ChainToleranceFeet)
            {
                throw new InvalidOperationException(
                    $"首柱起始位置 {request.StartOffsetMm:F0} mm 已超过路线总长 {totalLength * 304.8:F0} mm。");
            }

            List<double> distances = CalculateDistances(
                totalLength,
                startOffsetFeet,
                spacingFeet,
                request.IncludeEndColumn);
            if (distances.Count == 0)
                throw new InvalidOperationException("没有计算出可布置的柱位置。");

            var createdIds = new List<ElementId>();
            using (Transaction transaction = new Transaction(_doc, "沿线布置柱"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动沿线布柱事务，模型未修改。");
                try
                {
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }

                    // Autodesk documents both architectural Columns and Structural Columns
                    // with the level-based overload and StructuralType.Column.
                    const StructuralType structuralType = StructuralType.Column;

                    foreach (double distance in distances)
                    {
                        PlacementFrame placement = EvaluateFrameAtDistance(effectiveCurves, distance);
                        XYZ routePoint = placement.Point;
                        XYZ insertionPoint = new XYZ(routePoint.X, routePoint.Y, baseLevel.ProjectElevation);

                        FamilyInstance instance = _doc.Create.NewFamilyInstance(
                            insertionPoint,
                            symbol,
                            baseLevel,
                            structuralType);

                        RotateColumnToRoute(
                            instance,
                            insertionPoint,
                            placement.Tangent,
                            request.RotationOffsetDegrees);

                        SetRequiredElementIdParameter(
                            instance,
                            BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                            BuiltInParameter.FAMILY_LEVEL_PARAM,
                            baseLevel.Id,
                            "Base Level");
                        SetRequiredDoubleParameter(
                            instance,
                            BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,
                            baseOffsetFeet,
                            "Base Offset");
                        SetRequiredElementIdParameter(
                            instance,
                            BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
                            null,
                            topLevel.Id,
                            "Top Level");
                        SetRequiredDoubleParameter(
                            instance,
                            BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM,
                            topOffsetFeet,
                            "Top Offset");

                        createdIds.Add(instance.Id);
                    }

                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"Revit 未提交柱布置事务，状态：{status}。");
                }
                catch (Exception writeError)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"沿线布柱写入失败且回滚无法确认：{rollbackError.Message}；原始错误：{writeError.Message}", writeError);
                        }
                        throw new InvalidOperationException("沿线布柱失败，整批已回滚：" + writeError.Message, writeError);
                    }
                    throw new InvalidOperationException(
                        "沿线布柱事务结果无法确认，请检查模型后再重试：" + writeError.Message, writeError);
                }
            }

            var result = new ColumnAlongPathPlacementResult
            {
                CreatedIds = createdIds.AsReadOnly(),
                SpacingMm = request.SpacingMm,
                RotationOffsetDegrees = request.RotationOffsetDegrees,
                FirstStationM = request.RouteStartMileageM + distances.First() * 0.3048,
                LastStationM = request.RouteStartMileageM + distances.Last() * 0.3048,
                ReadbackConfirmed = true
            };
            // Readback after Commit must never enter the transaction rollback handler.
            try
            {
                foreach (ElementId createdId in createdIds)
                {
                    FamilyInstance placed = _doc.GetElement(createdId) as FamilyInstance;
                    if (placed == null || !placed.IsValidObject ||
                        placed.GetTypeId().GetIdValue() != request.ColumnTypeId)
                        throw new InvalidOperationException($"柱 ID {createdId.GetIdValue()} 的实例/类型无法确认。");

                    // Validate the committed level association and offset, not just the type.
                    // Level.Elevation can be relative to Shared coordinates; ProjectElevation
                    // is the project-origin coordinate used by NewFamilyInstance's XYZ.
                    Parameter? placedBase = placed.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)
                        ?? placed.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    Parameter? placedTop = placed.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    Parameter? placedBaseOffset = placed.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                    Parameter? placedTopOffset = placed.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                    const double positionToleranceFeet = 1.0 / 304.8;
                    if (placedBase == null || placedBase.AsElementId() != baseLevel.Id ||
                        placedTop == null || placedTop.AsElementId() != topLevel.Id ||
                        placedBaseOffset == null ||
                        Math.Abs(placedBaseOffset.AsDouble() - baseOffsetFeet) > positionToleranceFeet ||
                        placedTopOffset == null ||
                        Math.Abs(placedTopOffset.AsDouble() - topOffsetFeet) > positionToleranceFeet)
                    {
                        throw new InvalidOperationException($"柱 ID {createdId.GetIdValue()} 的 Base/Top Level 或 Offset 回读不一致。");
                    }
                }
            }
            catch (Exception readbackError)
            {
                result.ReadbackConfirmed = false;
                result.ReadbackWarning = "沿线布柱已提交，但模型回读无法确认：" + readbackError.Message +
                    "。请先核对模型，不要直接重复布置。";
            }
            return result;
        }

        private static void ValidateFinite(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(label + "必须为有效数字。");
        }

        private static bool IsApproximatelyHorizontal(Curve curve)
        {
            IList<XYZ> points = curve.Tessellate();
            if (points.Count == 0)
                return false;

            double referenceZ = points[0].Z;
            return points.All(point => Math.Abs(point.Z - referenceZ) <= ChainToleranceFeet);
        }

        private static List<Curve> BuildOpenContinuousChain(IList<Curve> curves, Curve firstSelectedCurve)
        {
            if (curves.Count == 1)
            {
                if (curves[0].GetEndPoint(0).DistanceTo(curves[0].GetEndPoint(1)) <= ChainToleranceFeet)
                    throw new InvalidOperationException("所选模型线为闭合曲线，当前功能需要一条具有明确首尾的开放路线。");
                return new List<Curve> { curves[0].CreateTransformed(Transform.Identity) };
            }

            var clusters = new List<EndpointCluster>();
            foreach (Curve curve in curves)
            {
                AddEndpoint(clusters, curve.GetEndPoint(0));
                AddEndpoint(clusters, curve.GetEndPoint(1));
            }

            EndpointCluster[] branchClusters = clusters.Where(c => c.Count > 2).ToArray();
            if (branchClusters.Length > 0)
            {
                throw new InvalidOperationException(
                    "所选模型线存在分叉节点。请只选择一条没有分叉的连续路线。");
            }

            EndpointCluster[] openEnds = clusters.Where(c => c.Count == 1).ToArray();
            if (openEnds.Length == 0)
                throw new InvalidOperationException("所选模型线形成闭合环，当前功能需要一条具有明确首尾的开放路线。");
            if (openEnds.Length != 2 || clusters.Any(c => c.Count != 1 && c.Count != 2))
                throw new InvalidOperationException("所选模型线不是一条完整连续的开放路线，请检查断点或重复线。");

            XYZ preferredPoint = firstSelectedCurve.GetEndPoint(0);
            XYZ startPoint = openEnds.OrderBy(c => c.Point.DistanceTo(preferredPoint)).First().Point;

            var remaining = curves.Select(c => c.CreateTransformed(Transform.Identity)).ToList();
            var result = new List<Curve>();
            XYZ currentPoint = startPoint;

            while (remaining.Count > 0)
            {
                int matchIndex = -1;
                bool reverse = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    Curve candidate = remaining[i];
                    if (candidate.GetEndPoint(0).DistanceTo(currentPoint) <= ChainToleranceFeet)
                    {
                        matchIndex = i;
                        reverse = false;
                        break;
                    }
                    if (candidate.GetEndPoint(1).DistanceTo(currentPoint) <= ChainToleranceFeet)
                    {
                        matchIndex = i;
                        reverse = true;
                        break;
                    }
                }

                if (matchIndex < 0)
                    throw new InvalidOperationException("所选模型线之间存在断点，无法连接成连续多段线。");

                Curve matched = remaining[matchIndex];
                remaining.RemoveAt(matchIndex);
                Curve oriented = reverse ? matched.CreateReversed() : matched;
                result.Add(oriented);
                currentPoint = oriented.GetEndPoint(1);
            }

            for (int i = 0; i < result.Count - 1; i++)
            {
                if (result[i].GetEndPoint(1).DistanceTo(result[i + 1].GetEndPoint(0)) > ChainToleranceFeet)
                    throw new InvalidOperationException("模型线排序后仍存在断点，无法生成连续路线。");
            }

            return result;
        }

        private static void AddEndpoint(List<EndpointCluster> clusters, XYZ point)
        {
            EndpointCluster? existing = clusters.FirstOrDefault(c => c.Point.DistanceTo(point) <= ChainToleranceFeet);
            if (existing == null)
            {
                clusters.Add(new EndpointCluster(point));
            }
            else
            {
                existing.Add(point);
            }
        }

        private static List<Curve> ReverseChain(IEnumerable<Curve> curves)
        {
            return curves
                .Reverse()
                .Select(curve => curve.CreateReversed())
                .ToList();
        }

        private static List<double> CalculateDistances(
            double totalLength,
            double startOffset,
            double spacing,
            bool includeEndColumn)
        {
            var distances = new List<double>();
            if (spacing <= PointToleranceFeet || startOffset > totalLength + ChainToleranceFeet)
                return distances;

            // Bound the requested batch before building a list to avoid freezing Revit
            // on submillimetre spacing or a nearly-zero floating-point increment.
            const int maxColumnsPerBatch = 10000;
            double first = Math.Min(startOffset, totalLength);
            double estimatedCount = Math.Floor((totalLength - first + PointToleranceFeet) / spacing) + 1.0;
            if (double.IsNaN(estimatedCount) || double.IsInfinity(estimatedCount) || estimatedCount > maxColumnsPerBatch)
                throw new InvalidOperationException($"预计超过单次 {maxColumnsPerBatch} 根柱限制，请增大间距或分段布置。");

            for (int index = 0; index < (int)estimatedCount; index++)
            {
                double current = first + index * spacing;
                if (current > totalLength + PointToleranceFeet)
                    break;
                distances.Add(Math.Min(current, totalLength));
            }

            if (includeEndColumn
                && distances.Count > 0
                && totalLength - distances[distances.Count - 1] > ChainToleranceFeet)
            {
                if (distances.Count >= maxColumnsPerBatch)
                    throw new InvalidOperationException($"含末端补柱后超过单次 {maxColumnsPerBatch} 根柱限制。");
                distances.Add(totalLength);
            }

            return distances;
        }

        private static PlacementFrame EvaluateFrameAtDistance(IList<Curve> curves, double distance)
        {
            double totalLength = curves.Sum(c => c.Length);
            double cappedDistance = Math.Max(0.0, Math.Min(distance, totalLength));
            double accumulated = 0.0;

            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];
                double curveEnd = accumulated + curve.Length;
                if (cappedDistance <= curveEnd + PointToleranceFeet || i == curves.Count - 1)
                {
                    double localLength = Math.Max(0.0, Math.Min(cappedDistance - accumulated, curve.Length));
                    double normalizedParameter = GetNormalizedParameterAtArcLength(curve, localLength);
                    XYZ point = curve.Evaluate(normalizedParameter, true);
                    Transform derivatives = curve.ComputeDerivatives(normalizedParameter, true);
                    XYZ tangent = derivatives.BasisX;

                    if (tangent == null || tangent.IsZeroLength())
                    {
                        tangent = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                    }

                    tangent = new XYZ(tangent.X, tangent.Y, 0.0);
                    if (tangent.IsZeroLength())
                    {
                        throw new InvalidOperationException(
                            $"路线第 {i + 1} 段在布置位置处无法计算水平切线，不能确定柱旋转方向。");
                    }

                    return new PlacementFrame(point, tangent.Normalize());
                }
                accumulated = curveEnd;
            }

            Curve lastCurve = curves[curves.Count - 1];
            XYZ fallbackTangent = lastCurve.GetEndPoint(1) - lastCurve.GetEndPoint(0);
            fallbackTangent = new XYZ(fallbackTangent.X, fallbackTangent.Y, 0.0);
            if (fallbackTangent.IsZeroLength())
                fallbackTangent = XYZ.BasisX;

            return new PlacementFrame(lastCurve.GetEndPoint(1), fallbackTangent.Normalize());
        }

        private static void RotateColumnToRoute(
            FamilyInstance instance,
            XYZ insertionPoint,
            XYZ routeTangent,
            double rotationOffsetDegrees)
        {
            XYZ flatTangent = new XYZ(routeTangent.X, routeTangent.Y, 0.0);
            if (flatTangent.IsZeroLength())
                throw new InvalidOperationException("无法计算柱所在位置的路线方向。");

            flatTangent = flatTangent.Normalize();
            double targetAngle = XYZ.BasisX.AngleOnPlaneTo(flatTangent, XYZ.BasisZ)
                + rotationOffsetDegrees * Math.PI / 180.0;

            double currentAngle = 0.0;
            if (instance.Location is LocationPoint locationPoint)
                currentAngle = locationPoint.Rotation;

            double deltaAngle = NormalizeSignedAngle(targetAngle - currentAngle);
            if (Math.Abs(deltaAngle) <= 1e-9)
                return;

            Line rotationAxis = Line.CreateBound(insertionPoint, insertionPoint + XYZ.BasisZ);
            try
            {
                ElementTransformUtils.RotateElement(instance.Document, instance.Id, rotationAxis, deltaAngle);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"柱实例 ID {instance.Id.GetIdValue()} 无法沿路线旋转。请检查柱族是否允许绕竖向轴旋转。",
                    ex);
            }
        }

        private static double NormalizeSignedAngle(double angle)
        {
            const double twoPi = Math.PI * 2.0;
            angle %= twoPi;
            if (angle > Math.PI) angle -= twoPi;
            if (angle < -Math.PI) angle += twoPi;
            return angle;
        }

        private static double GetNormalizedParameterAtArcLength(Curve curve, double targetLength)
        {
            if (targetLength <= 0.0) return 0.0;
            if (targetLength >= curve.Length - PointToleranceFeet) return 1.0;
            if (curve is Line || curve is Arc) return targetLength / curve.Length;

            const int steps = 300;
            var cumulative = new double[steps + 1];
            XYZ previous = curve.Evaluate(0.0, true);
            for (int i = 1; i <= steps; i++)
            {
                XYZ current = curve.Evaluate(i / (double)steps, true);
                cumulative[i] = cumulative[i - 1] + previous.DistanceTo(current);
                previous = current;
            }

            double numericalTarget = targetLength / curve.Length * cumulative[steps];
            for (int i = 0; i < steps; i++)
            {
                if (numericalTarget <= cumulative[i + 1])
                {
                    double segmentLength = cumulative[i + 1] - cumulative[i];
                    double fraction = segmentLength <= PointToleranceFeet
                        ? 0.0
                        : (numericalTarget - cumulative[i]) / segmentLength;
                    return (i + fraction) / steps;
                }
            }

            return 1.0;
        }

        private static void SetRequiredElementIdParameter(
            FamilyInstance instance,
            BuiltInParameter primary,
            BuiltInParameter? fallback,
            ElementId value,
            string displayName)
        {
            Parameter? parameter = instance.get_Parameter(primary);
            if ((parameter == null || parameter.IsReadOnly) && fallback.HasValue)
                parameter = instance.get_Parameter(fallback.Value);

            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                throw new InvalidOperationException($"柱族不支持写入 {displayName} 参数，请更换标准标高型柱族。");

            if (!parameter.Set(value))
                throw new InvalidOperationException($"Revit 拒绝写入 {displayName} 参数。");
        }

        private static void SetRequiredDoubleParameter(
            FamilyInstance instance,
            BuiltInParameter parameterId,
            double value,
            string displayName)
        {
            Parameter? parameter = instance.get_Parameter(parameterId);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.Double)
                throw new InvalidOperationException($"柱族不支持写入 {displayName} 参数，请更换标准标高型柱族。");

            if (!parameter.Set(value))
                throw new InvalidOperationException($"Revit 拒绝写入 {displayName} 参数。");
        }

        internal static string FormatStation(double stationMeters)
        {
            string sign = stationMeters < 0 ? "-" : string.Empty;
            double absolute = Math.Abs(stationMeters);
            int kilometers = (int)Math.Floor(absolute / 1000.0);
            double meters = absolute - kilometers * 1000.0;
            return $"{sign}K{kilometers}+{meters:000.000}";
        }

        private sealed class PlacementFrame
        {
            public PlacementFrame(XYZ point, XYZ tangent)
            {
                Point = point;
                Tangent = tangent;
            }

            public XYZ Point { get; }
            public XYZ Tangent { get; }
        }

        private sealed class EndpointCluster
        {
            private XYZ _sum;

            public EndpointCluster(XYZ point)
            {
                _sum = point;
                Count = 1;
                Point = point;
            }

            public int Count { get; private set; }
            public XYZ Point { get; private set; }

            public void Add(XYZ point)
            {
                _sum += point;
                Count++;
                Point = _sum / Count;
            }
        }
    }

    public sealed class ModelLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            return element is ModelCurve;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
