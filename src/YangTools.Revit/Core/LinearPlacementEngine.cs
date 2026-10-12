using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace YangTools.Revit.Core
{
    public class LinearPlacementEngine
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public LinearPlacementEngine(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
        }

        public List<Curve>? PickAndExtractCurves()
        {
            IList<Element> selectedElems = _uidoc.Selection.PickElementsByRectangle(new GenericCurveFilter(), "框选路线 (支持模型线、体量、CAD链接等)");
            if (selectedElems == null || selectedElems.Count == 0) return null;

            List<Curve> allCurves = new List<Curve>();
            var geomOptions = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            foreach (var elem in selectedElems)
            {
                var geom = elem.get_Geometry(geomOptions);
                if (geom != null)
                {
                    ExtractCurves(geom, Transform.Identity, allCurves);
                }
            }

            if (allCurves.Count == 0) return null;

            List<Curve> chained = SortAndChainCurves(allCurves);
            int omittedCount = allCurves.Count - chained.Count;
            if (omittedCount > 0)
                throw new InvalidOperationException(
                    $"路线未完全连接：共 {allCurves.Count} 段，仅 {chained.Count} 段成功连接，" +
                    $"另 {omittedCount} 段未连接。为防止静默遗漏，请重新选择完整连续路线。");

            return chained;
        }

        /// <summary>Only call inside the document-bound Revit ExternalEvent.</summary>
        public LinearPlacementResult ExecutePlacement(LinearPlacementRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_doc.IsReadOnly) throw new InvalidOperationException("当前文档为只读，不能创建族实例。");
            if (request.ChainedCurves.Count == 0)
                throw new InvalidOperationException("请先拾取连续路线。");

            FamilySymbol symbol = _doc.GetElement(ElementIdExtensions.CreateId(request.SymbolId)) as FamilySymbol
                ?? throw new InvalidOperationException("所选族类型已失效，请重新选择。");
            Level targetLevel = _doc.GetElement(ElementIdExtensions.CreateId(request.LevelId)) as Level
                ?? throw new InvalidOperationException("所选基准标高已失效，请重新选择。");

            ValidateFinite(request.StartElevationMm, "起点高程");
            ValidateFinite(request.EndElevationMm, "终点高程");
            ValidateFinite(request.StartValue, "布置起点");
            ValidateFinite(request.EndValue, "布置终点");
            ValidateFinite(request.StepValue, "布置间距或数量");
            ValidateFinite(request.RotationOffset, "旋转偏移");
            if (request.ChainedCurves.Any(curve => curve == null || !curve.IsBound || curve.Length <= 0.001))
                throw new InvalidOperationException("选中的路线包含无效或太短的曲线，请重新选择。");

            // Do not silently straighten, drop disconnected segments, or skip an invalid chain.
            for (int i = 1; i < request.ChainedCurves.Count; i++)
            {
                if (request.ChainedCurves[i - 1].GetEndPoint(1).DistanceTo(
                    request.ChainedCurves[i].GetEndPoint(0)) > 0.005)
                    throw new InvalidOperationException("路径包含不连续曲线段，已阻止布置以免遗漏或跨越断点。");
            }

            List<PlacementData> placements = CalculatePlacements(request.ChainedCurves, request);
            if (placements.Count == 0)
                throw new InvalidOperationException("没有计算出布置点，请检查起点、终点和间距设置。");
            ApplyElevationProfile(placements, targetLevel, request);
            var createdIds = new List<ElementId>();
            var expectedPoints = placements.Select(data => data.Point).ToList();
            using (Transaction transaction = new Transaction(_doc, "线性布置族实例"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动线性布置事务，模型未修改。");
                try
                {
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                    }
                    foreach (PlacementData data in placements)
                    {
                        FamilyInstance instance = FamilyPlacementHelper.CreateAtLevelRelativeElevation(
                            _doc, symbol, targetLevel, data.Point, data.RelativeElevationFeet, "线性布置");
                        if (request.AlignToCurve)
                            AlignInstanceToCurve(instance, data, request);
                        createdIds.Add(instance.Id);
                    }
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"线性布置事务未确认提交：{status}。");
                }
                catch (Exception writeError)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务未返回 RolledBack 状态。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"线性布置写入失败且回滚无法确认：{rollbackError.Message}；原始错误：{writeError.Message}", writeError);
                        }
                        throw new InvalidOperationException("线性布置失败，整批已回滚：" + writeError.Message, writeError);
                    }
                    throw new InvalidOperationException(
                        "线性布置事务结果无法确认，请先检查模型再重试：" + writeError.Message, writeError);
                }
            }

            // Model commit is final; readback failures must never be reported as a transaction rollback.
            var result = new LinearPlacementResult { CreatedIds = createdIds.AsReadOnly() };
            try
            {
                for (int i = 0; i < createdIds.Count; i++)
                {
                    ElementId id = createdIds[i];
                    FamilyInstance instance = _doc.GetElement(id) as FamilyInstance
                        ?? throw new InvalidOperationException($"提交后找不到实例 {id}。");
                    if (instance.GetTypeId() != symbol.Id)
                        throw new InvalidOperationException($"实例 {id} 的族类型与请求不一致。");
                    if (!(instance.Location is LocationPoint location))
                        throw new InvalidOperationException($"实例 {id} 没有可回读的位置。");
                    const double positionToleranceFeet = 1.0 / 304.8;
                    if (location.Point.DistanceTo(expectedPoints[i]) > positionToleranceFeet)
                        throw new InvalidOperationException($"实例 {id} 的真实位置与目标点偏差超过 1 mm。");
                }
                result.ReadbackConfirmed = true;
            }
            catch (Exception ex)
            {
                result.ReadbackWarning = "事务已提交，但实例回读无法确认：" + ex.Message;
            }
            return result;
        }

        private static void ValidateFinite(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(label + "必须是有限数字。");
        }

        private void ApplyElevationProfile(
            List<PlacementData> placements,
            Level targetLevel,
            LinearPlacementRequest request)
        {
            if (placements.Count == 0)
                return;

            const double millimetersPerFoot = 304.8;
            double startRelativeFeet = request.StartElevationMm / millimetersPerFoot;
            double endRelativeFeet = request.EndElevationMm / millimetersPerFoot;

            // A single placement uses the starting elevation; the caller's UI reports this case.

            if (double.IsNaN(startRelativeFeet) || double.IsInfinity(startRelativeFeet)
                || double.IsNaN(endRelativeFeet) || double.IsInfinity(endRelativeFeet))
            {
                throw new InvalidOperationException("起点高程和终点高程必须是有效数字。");
            }

            // “起点/终点高程”对应实际首尾两个已放置轮廓，而不是默认项目 Z=0。
            // 世界 Z 必须使用 ProjectElevation：XYZ 属于项目内部坐标，不能使用可能以共享坐标显示的 Level.Elevation。
            // 世界 Z = 所选标高.ProjectElevation + 相对高程。
            double firstDistance = placements[0].DistanceAlongPath;
            double lastDistance = placements[placements.Count - 1].DistanceAlongPath;
            double distanceSpan = lastDistance - firstDistance;
            double relativeRise = endRelativeFeet - startRelativeFeet;
            double slopePerPathFoot = Math.Abs(distanceSpan) > 1e-9
                ? relativeRise / distanceSpan
                : 0.0;

            for (int i = 0; i < placements.Count; i++)
            {
                PlacementData data = placements[i];
                double ratio = placements.Count == 1 || Math.Abs(distanceSpan) <= 1e-9
                    ? 0.0
                    : (data.DistanceAlongPath - firstDistance) / distanceSpan;
                ratio = Math.Max(0.0, Math.Min(1.0, ratio));

                double relativeElevationFeet = startRelativeFeet + relativeRise * ratio;
                double worldZ = targetLevel.ProjectElevation + relativeElevationFeet;
                data.RelativeElevationFeet = relativeElevationFeet;
                data.Point = new XYZ(data.Point.X, data.Point.Y, worldZ);

                // 当用户选择“不保持垂直”时，朝向应跟随新的线性高程坡度，
                // 而不是继续使用原路线本身的旧 Z 坡度。KeepVertical=true 时后续会自动清零 Z。
                XYZ originalTangent = data.Tangent;
                XYZ profiledTangent = new XYZ(originalTangent.X, originalTangent.Y, slopePerPathFoot);
                if (!profiledTangent.IsZeroLength())
                    data.Tangent = profiledTangent.Normalize();
            }

            DiagnosticLog.WriteInfo(
                "线性布置",
                $"高程基准={targetLevel.Name}；项目坐标高程={targetLevel.ProjectElevation:G8} ft；显示高程={targetLevel.Elevation:G8} ft；" +
                $"起点相对高程={request.StartElevationMm:G8} mm；" +
                $"终点相对高程={request.EndElevationMm:G8} mm；" +
                $"首点世界Z={placements[0].Point.Z:G8} ft；" +
                $"末点世界Z={placements[placements.Count - 1].Point.Z:G8} ft；" +
                $"实例数={placements.Count}");
        }

        private void AlignInstanceToCurve(
            FamilyInstance instance,
            PlacementData data,
            LinearPlacementRequest request)
        {
            XYZ tangent = data.Tangent;
            double offsetRad = request.RotationOffset * Math.PI / 180.0;

            if (request.KeepVertical)
            {
                tangent = new XYZ(tangent.X, tangent.Y, 0);
                tangent = tangent.IsZeroLength() ? XYZ.BasisX : tangent.Normalize();

                double angle = XYZ.BasisX.AngleOnPlaneTo(tangent, XYZ.BasisZ) + offsetRad;
                Line axis = Line.CreateBound(data.Point, data.Point + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(_doc, instance.Id, axis, angle);
                return;
            }

            XYZ flatTangent = new XYZ(tangent.X, tangent.Y, 0);
            flatTangent = flatTangent.IsZeroLength() ? XYZ.BasisX : flatTangent.Normalize();

            double angleZ = XYZ.BasisX.AngleOnPlaneTo(flatTangent, XYZ.BasisZ) + offsetRad;
            Line axisZ = Line.CreateBound(data.Point, data.Point + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, instance.Id, axisZ, angleZ);

            double pitchAngle = tangent.AngleTo(flatTangent);
            if (tangent.Z < 0) pitchAngle = -pitchAngle;
            if (Math.Abs(pitchAngle) <= 0.001)
                return;

            XYZ pitchAxisVec = XYZ.BasisZ.CrossProduct(flatTangent);
            if (pitchAxisVec.IsZeroLength())
                return;

            Line pitchAxis = Line.CreateBound(data.Point, data.Point + pitchAxisVec.Normalize());
            ElementTransformUtils.RotateElement(_doc, instance.Id, pitchAxis, pitchAngle);
        }

        private void ExtractCurves(GeometryObject geomObj, Transform transform, List<Curve> curves)
        {
            if (geomObj is Curve curve)
            {
                if (curve.Length > 0.001)
                {
                    curves.Add(curve.CreateTransformed(transform));
                }
            }
            else if (geomObj is GeometryElement geomElem)
            {
                foreach (var gObj in geomElem)
                {
                    ExtractCurves(gObj, transform, curves);
                }
            }
            else if (geomObj is PolyLine polyLine)
            {
                var points = polyLine.GetCoordinates();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ p0 = transform.OfPoint(points[i]);
                    XYZ p1 = transform.OfPoint(points[i + 1]);
                    if (p0.DistanceTo(p1) > 0.001)
                        curves.Add(Line.CreateBound(p0, p1));
                }
            }
            else if (geomObj is GeometryInstance geomInst)
            {
                // GetInstanceGeometry() 已返回项目坐标系中的几何；再次套用
                // geomInst.Transform 会把旋转/位移应用两次。
                var instGeom = geomInst.GetInstanceGeometry();
                if (instGeom != null)
                {
                    ExtractCurves(instGeom, transform, curves);
                }
            }
        }

        private List<Curve> SortAndChainCurves(List<Curve> inputCurves)
        {
            if (inputCurves.Count <= 1) return inputCurves;

            var result = new List<Curve>();
            var remaining = new List<Curve>(inputCurves);

            // Start with the longest curve as seed
            var seed = remaining.OrderByDescending(c => c.Length).First();
            result.Add(seed);
            remaining.Remove(seed);

            double tolerance = 0.005; // ~1.5mm

            bool added = true;
            while (added && remaining.Count > 0)
            {
                added = false;
                XYZ currentStart = result.First().GetEndPoint(0);
                XYZ currentEnd = result.Last().GetEndPoint(1);

                for (int i = 0; i < remaining.Count; i++)
                {
                    var c = remaining[i];
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    // Try attach to end
                    if (p0.DistanceTo(currentEnd) < tolerance)
                    {
                        result.Add(c);
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                    if (p1.DistanceTo(currentEnd) < tolerance)
                    {
                        result.Add(c.CreateReversed());
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }

                    // Try attach to start
                    if (p1.DistanceTo(currentStart) < tolerance)
                    {
                        result.Insert(0, c);
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                    if (p0.DistanceTo(currentStart) < tolerance)
                    {
                        result.Insert(0, c.CreateReversed());
                        remaining.RemoveAt(i);
                        added = true;
                        break;
                    }
                }
            }

            return result;
        }

        private List<PlacementData> CalculatePlacements(IReadOnlyList<Curve> chainedCurves, LinearPlacementRequest request)
        {
            var dataList = new List<PlacementData>();
            List<Curve> effectiveCurves = request.ReversePath
                ? chainedCurves.AsEnumerable().Reverse().Select(curve => curve.CreateReversed()).ToList()
                : chainedCurves.ToList();

            double[] curveLengths = effectiveCurves.Select(c => c.Length).ToArray();
            double[] cumulativeLengths = new double[curveLengths.Length + 1];
            cumulativeLengths[0] = 0;
            for (int i = 0; i < curveLengths.Length; i++)
                cumulativeLengths[i + 1] = cumulativeLengths[i] + curveLengths[i];

            // Inputs are distances (mm) or normalized station positions (0..1).
            double totalLength = effectiveCurves.Sum(c => c.Length);
            if (totalLength <= 0.001 || double.IsInfinity(totalLength))
                throw new InvalidOperationException("路线长度无效或过短。");

            double scale = request.IsByDistance ? 1.0 / 304.8 : totalLength;
            double trueStart = request.StartValue * scale;
            double trueEnd = request.EndValue * scale;
            if (!request.IsByDistance && (request.StartValue < 0 || request.EndValue > 1))
                throw new InvalidOperationException("参数模式下，起点和终点必须位于 0～1 范围内。");
            if (trueStart < 0 || trueEnd > totalLength + 1e-6)
                throw new InvalidOperationException("布置起终点超出路线有效长度。");
            if (trueEnd < trueStart)
                throw new InvalidOperationException("终点必须不小于起点；反向布置请使用“反转路线首尾”。");
            if (trueStart > totalLength)
                throw new InvalidOperationException("布置起点超过路线总长。");
            trueEnd = Math.Min(trueEnd, totalLength);

            const int maxPlacements = 10000;
            int numPoints;
            double trueStep;
            if (request.IsBySpacing)
            {
                trueStep = request.StepValue * scale;
                if (trueStep <= 0.001 || double.IsInfinity(trueStep))
                    throw new InvalidOperationException("布置间距必须大于约 0.305 mm。");
                double estimatedCount = Math.Floor((trueEnd - trueStart) / trueStep + 1e-9) + 1;
                if (estimatedCount > maxPlacements)
                    throw new InvalidOperationException($"预计布置 {estimatedCount:F0} 个实例，超过单次 {maxPlacements} 个上限，请增大间距。");
                numPoints = (int)estimatedCount;
            }
            else
            {
                if (request.StepValue < 1 || request.StepValue > maxPlacements
                    || Math.Abs(request.StepValue - Math.Round(request.StepValue)) > 1e-8)
                    throw new InvalidOperationException($"固定数量必须是 1～{maxPlacements} 的整数。");
                numPoints = (int)Math.Round(request.StepValue);
                if (numPoints > 1 && trueEnd - trueStart <= 0.001)
                    throw new InvalidOperationException("固定数量大于 1 时，起终点距离必须大于约 0.305 mm。");
                trueStep = numPoints == 1 ? 0 : (trueEnd - trueStart) / (numPoints - 1);
            }

            void AddPlacementAtDistance(double distance)
            {
                double cappedDistance = Math.Min(Math.Max(distance, 0.0), totalLength);

                int curveIdx = effectiveCurves.Count - 1;
                for (int j = 0; j < cumulativeLengths.Length - 1; j++)
                {
                    if (cappedDistance >= cumulativeLengths[j]
                        && cappedDistance <= cumulativeLengths[j + 1] + 0.0001)
                    {
                        curveIdx = j;
                        break;
                    }
                }

                Curve targetCurve = effectiveCurves[curveIdx];
                double localLength = cappedDistance - cumulativeLengths[curveIdx];
                double localParam = GetNormalizedParameterAtArcLength(targetCurve, localLength);

                XYZ point = targetCurve.Evaluate(localParam, true);
                Transform derivatives = targetCurve.ComputeDerivatives(localParam, true);
                XYZ tangent = derivatives.BasisX;
                if (tangent.IsZeroLength())
                    throw new InvalidOperationException("路线出现零切向量，无法确定实例方向。");
                tangent = tangent.Normalize();

                dataList.Add(new PlacementData
                {
                    Point = point,
                    Tangent = tangent,
                    DistanceAlongPath = cappedDistance
                });
            }

            double lastPlacedDistance = double.NaN;
            for (int i = 0; i < numPoints; i++)
            {
                double distance = Math.Min(trueStart + i * trueStep, trueEnd);
                AddPlacementAtDistance(distance);
                lastPlacedDistance = distance;
            }

            if (request.IsBySpacing
                && request.IncludeRemainderEndpoint
                && (double.IsNaN(lastPlacedDistance) || trueEnd - lastPlacedDistance > 0.0001))
            {
                if (dataList.Count >= maxPlacements)
                    throw new InvalidOperationException($"包含终点后数量超过 {maxPlacements} 个实例上限。");
                AddPlacementAtDistance(trueEnd);
            }

            return dataList;
        }

        private double GetNormalizedParameterAtArcLength(Curve curve, double targetLength)
        {
            if (targetLength <= 0) return 0.0;
            if (targetLength >= curve.Length - 1e-5) return 1.0;

            if (curve is Line || curve is Arc)
            {
                return targetLength / curve.Length;
            }

            int steps = 200;
            double[] dists = new double[steps + 1];
            dists[0] = 0;
            XYZ prevPt = curve.Evaluate(0.0, true);
            for (int i = 1; i <= steps; i++)
            {
                XYZ pt = curve.Evaluate(i / (double)steps, true);
                dists[i] = dists[i - 1] + prevPt.DistanceTo(pt);
                prevPt = pt;
            }

            double totalNumLen = dists[steps];
            double targetNumLen = targetLength / curve.Length * totalNumLen;

            for (int i = 0; i < steps; i++)
            {
                if (targetNumLen >= dists[i] && targetNumLen <= dists[i + 1])
                {
                    double segmentLen = dists[i + 1] - dists[i];
                    double fraction = (segmentLen < 1e-9) ? 0 : (targetNumLen - dists[i]) / segmentLen;
                    return (i + fraction) / steps;
                }
            }

            return 1.0;
        }
    }

    public class PlacementData
    {
        public XYZ Point { get; set; } = XYZ.Zero;
        public XYZ Tangent { get; set; } = XYZ.BasisX;
        public double DistanceAlongPath { get; set; }
        public double RelativeElevationFeet { get; set; }
    }

    public class GenericCurveFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category == null) return false;
            // Filter to only elements likely to contain curves (lines, links, masses, families)
            return elem is CurveElement || elem is ImportInstance || elem is FamilyInstance || elem is DirectShape || elem.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Mass;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
