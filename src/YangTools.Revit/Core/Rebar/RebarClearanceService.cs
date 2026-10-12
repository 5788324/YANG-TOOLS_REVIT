using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// R09 read-only rebar-to-rebar clash / clear-spacing screening.
    /// Hard clash and clear spacing are intentionally distinct:
    /// surface gap = minimum centerline distance - (radiusA + radiusB).
    /// Straight line geometry is measured exactly. Arc/other curves are tessellated and
    /// explicitly reported as screening approximation rather than code-compliance proof.
    /// </summary>
    public static class RebarClearanceService
    {
        private const int MaxRebarElements = 100;
        private const int MaxPhysicalBars = 2000;
        private const int MaxSegmentsPerPhysicalBar = 512;
        private const long MaxDetailedPairChecks = 250000;
        private const int HardMaxResults = 1000;
        private const double Epsilon = 1e-12;

        public static RebarClearanceReport Analyze(UIApplication uiapp, RebarClearanceRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            var report = new RebarClearanceReport
            {
                Success = false,
                ChangedModel = false,
                RevitVersion = uiapp.Application.VersionNumber ?? string.Empty,
                DocumentTitle = doc.Title ?? string.Empty,
                MinimumClearanceMm = request.MinimumClearanceMm,
                IncludeSameElement = request.IncludeSameElement,
                RequestedRebarCount = request.RebarIds?.Count ?? 0
            };

            if (request.MinimumClearanceMm < 0 || request.MinimumClearanceMm > 5000)
                return WithError(report, "MinimumClearanceMm 必须在 0..5000 mm 范围内。");
            if (request.MaxResults < 1 || request.MaxResults > HardMaxResults)
                return WithError(report, $"MaxResults 必须在 1..{HardMaxResults} 范围内。");

            List<long> inputIds = (request.RebarIds ?? new List<long>())
                .Where(value => value > 0)
                .Distinct()
                .Take(MaxRebarElements + 1)
                .ToList();
            if (inputIds.Count > MaxRebarElements)
                return WithError(report, $"单次最多分析 {MaxRebarElements} 个 Rebar 元素，请缩小范围。");
            if (inputIds.Count == 0)
                return WithError(report, "RebarIds 不能为空；R09 只分析显式指定的钢筋范围。");

            var rebars = new List<Autodesk.Revit.DB.Structure.Rebar>();
            foreach (long value in inputIds)
            {
                Element? element = doc.GetElement(ElementIdExtensions.CreateId(value));
                if (element is not Autodesk.Revit.DB.Structure.Rebar rebar)
                    return WithError(report, $"ElementId={value} 不是有效 Rebar。");
                rebars.Add(rebar);
            }

            if (!request.IncludeSameElement && rebars.Count < 2)
                return WithError(report, "至少需要 2 个 Rebar 元素；若要检查同一 Rebar set 内部净距，请设置 IncludeSameElement=true。");

            var physicalBars = new List<PhysicalBar>();
            foreach (Autodesk.Revit.DB.Structure.Rebar rebar in rebars)
            {
                double diameter = ReadDiameterInternal(doc.GetElement(rebar.GetTypeId()));
                if (diameter <= Epsilon)
                    return WithError(report, $"Rebar {rebar.Id.GetIdValue()} 无法读取有效直径。");

                int count;
                try { count = Math.Max(0, rebar.NumberOfBarPositions); }
                catch { count = 1; }
                if (count == 0) count = 1;

                for (int position = 0; position < count; position++)
                {
                    if (!DoesBarExist(rebar, position)) continue;
                    PhysicalBar? physical = BuildPhysicalBar(rebar, position, diameter, report.Warnings);
                    if (physical == null) continue;
                    physicalBars.Add(physical);
                    if (physicalBars.Count > MaxPhysicalBars)
                        return WithError(report, $"物理钢筋数量超过 {MaxPhysicalBars}，请缩小选择范围。");
                }
            }

            report.AnalyzedRebarCount = rebars.Count;
            report.PhysicalBarCount = physicalBars.Count;
            if (physicalBars.Count < 2)
                return WithError(report, "没有足够的有效 Bar Position 可进行距离分析。");

            double clearanceInternal = ToInternalMm(request.MinimumClearanceMm);
            var candidatePairs = new List<BarPair>();
            for (int i = 0; i < physicalBars.Count; i++)
            {
                PhysicalBar a = physicalBars[i];
                for (int j = i + 1; j < physicalBars.Count; j++)
                {
                    PhysicalBar b = physicalBars[j];
                    if (!request.IncludeSameElement && a.RebarId == b.RebarId) continue;
                    if (a.RebarId == b.RebarId && a.Position == b.Position) continue;

                    double expansion = a.Radius + b.Radius + clearanceInternal;
                    if (!BoxesMayConflict(a.Box, b.Box, expansion)) continue;
                    candidatePairs.Add(new BarPair(a, b));
                    if (candidatePairs.Count > MaxDetailedPairChecks)
                    {
                        report.Complete = false;
                        return WithError(report,
                            $"Broad-phase 候选超过 {MaxDetailedPairChecks} 对，为避免卡住 Revit 已停止。请缩小选择范围或分区检查。");
                    }
                }
            }

            report.BroadPhaseCandidatePairs = candidatePairs.Count;
            var allConflicts = new List<RebarClearanceConflict>();
            double? minGap = null;
            foreach (BarPair pair in candidatePairs)
            {
                report.DetailedPairChecks++;
                ClosestResult nearest = FindClosest(pair.A, pair.B);
                if (!nearest.Valid) continue;

                double centerMm = ToMm(nearest.Distance);
                double gapInternal = nearest.Distance - pair.A.Radius - pair.B.Radius;
                double gapMm = ToMm(gapInternal);
                if (!minGap.HasValue || gapMm < minGap.Value) minGap = gapMm;

                string status;
                if (gapInternal < -ToInternalMm(0.01)) status = "Clash";
                else if (gapInternal + ToInternalMm(0.01) < clearanceInternal) status = "ClearanceViolation";
                else continue;

                bool approximate = pair.A.HasApproximateGeometry || pair.B.HasApproximateGeometry || nearest.Approximate;
                if (approximate) report.HasApproximateGeometry = true;
                var conflict = new RebarClearanceConflict
                {
                    RebarAId = pair.A.RebarId,
                    RebarAUniqueId = pair.A.UniqueId,
                    BarAPosition = pair.A.Position,
                    BarADiameterMm = Math.Round(ToMm(pair.A.Radius * 2.0), 3),
                    RebarBId = pair.B.RebarId,
                    RebarBUniqueId = pair.B.UniqueId,
                    BarBPosition = pair.B.Position,
                    BarBDiameterMm = Math.Round(ToMm(pair.B.Radius * 2.0), 3),
                    CenterlineDistanceMm = Math.Round(centerMm, 3),
                    SurfaceGapMm = Math.Round(gapMm, 3),
                    Status = status,
                    GeometryAccuracy = approximate ? "TessellatedScreening" : "ExactLineSegments",
                    ClosestPointA = ToPoint(nearest.PointA),
                    ClosestPointB = ToPoint(nearest.PointB)
                };
                allConflicts.Add(conflict);
                if (status == "Clash") report.ClashCount++;
                else report.ClearanceViolationCount++;
            }

            report.MinimumSurfaceGapMm = minGap.HasValue ? Math.Round(minGap.Value, 3) : (double?)null;
            report.Conflicts = allConflicts
                .OrderBy(item => item.SurfaceGapMm)
                .Take(request.MaxResults)
                .ToList();
            if (allConflicts.Count > report.Conflicts.Count)
                report.Warnings.Add($"冲突结果共 {allConflicts.Count} 项，仅返回最严重的 {report.Conflicts.Count} 项；计数仍为完整计数。");
            if (report.HasApproximateGeometry)
                report.Warnings.Add("包含 Arc/非 Line 中心线，R09 使用 Revit Tessellate 后的线段做筛查；结果不能直接替代规范级净距验算。");
            report.Warnings.Add("R09 仅检查 Rebar↔Rebar；Coupler、预埋件、洞口、预应力与混凝土边界不在本轮范围。");
            report.Warnings.Add("R09 将硬碰撞与净距不足分开：SurfaceGap<0 为 Clash；0<=SurfaceGap<MinimumClearance 为 ClearanceViolation。");
            report.Success = true;
            return report;
        }

        private static PhysicalBar? BuildPhysicalBar(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            int position,
            double diameter,
            List<string> warnings)
        {
            IList<Curve> curves;
            string geometryMode;
            try { curves = GetActualPositionCenterlineCurves(rebar, position, out geometryMode); }
            catch (Exception ex)
            {
                warnings.Add($"Rebar {rebar.Id.GetIdValue()} position {position} 中心线读取失败：{ex.Message}");
                return null;
            }
            if (curves == null || curves.Count == 0) return null;

            var segments = new List<Segment3>();
            bool approximate = !string.Equals(geometryMode, "TransformedCenterline", StringComparison.Ordinal);
            foreach (Curve curve in curves)
            {
                if (curve is Line)
                {
                    segments.Add(new Segment3(curve.GetEndPoint(0), curve.GetEndPoint(1), false));
                }
                else
                {
                    approximate = true;
                    IList<XYZ> points = curve.Tessellate();
                    for (int i = 1; i < points.Count; i++)
                        segments.Add(new Segment3(points[i - 1], points[i], true));
                }
                if (segments.Count > MaxSegmentsPerPhysicalBar)
                    throw new InvalidOperationException($"Rebar {rebar.Id.GetIdValue()} position {position} 的离散线段超过 {MaxSegmentsPerPhysicalBar}。 ");
            }
            if (segments.Count == 0) return null;
            return new PhysicalBar(
                rebar.Id.GetIdValue(),
                rebar.UniqueId ?? string.Empty,
                position,
                diameter * 0.5,
                segments,
                approximate);
        }

        private static IList<Curve> GetActualPositionCenterlineCurves(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            int position,
            out string geometryMode)
        {
            MethodInfo? transformed = rebar.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.Name == "GetTransformedCenterlineCurves" && candidate.GetParameters().Length == 5);
            if (transformed != null)
            {
                object? raw = transformed.Invoke(rebar, new object[]
                {
                    false, false, false,
                    MultiplanarOption.IncludeAllMultiplanarCurves,
                    position
                });
                if (raw is IList<Curve> list)
                {
                    geometryMode = "TransformedCenterline";
                    return list;
                }
                if (raw is IEnumerable<Curve> enumerable)
                {
                    geometryMode = "TransformedCenterline";
                    return enumerable.ToList();
                }
            }

            IList<Curve> fallback = rebar.GetCenterlineCurves(
                false, false, false,
                MultiplanarOption.IncludeAllMultiplanarCurves,
                position);

            MethodInfo? transformMethod = rebar.GetType().GetMethod("GetBarPositionTransform", new[] { typeof(int) });
            if (transformMethod?.Invoke(rebar, new object[] { position }) is Transform transform)
            {
                geometryMode = "RawCenterline+BarPositionTransform";
                return fallback.Select(curve => curve.CreateTransformed(transform)).ToList();
            }

            geometryMode = "RawCenterlineFallback";
            return fallback;
        }

        private static bool DoesBarExist(Autodesk.Revit.DB.Structure.Rebar rebar, int position)
        {
            try
            {
                MethodInfo? method = rebar.GetType().GetMethod("DoesBarExistAtPosition", new[] { typeof(int) });
                if (method?.Invoke(rebar, new object[] { position }) is bool exists) return exists;
            }
            catch { }
            return true;
        }

        private static double ReadDiameterInternal(Element? barType)
        {
            if (barType == null) return 0;
            foreach (string propertyName in new[] { "BarModelDiameter", "BarDiameter", "NominalDiameter" })
            {
                try
                {
                    PropertyInfo? property = barType.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                    if (property?.GetValue(barType) is double value && value > Epsilon) return value;
                }
                catch { }
            }
            return 0;
        }

        private static bool BoxesMayConflict(Box3 a, Box3 b, double expansion)
        {
            return a.MinX - expansion <= b.MaxX && a.MaxX + expansion >= b.MinX &&
                   a.MinY - expansion <= b.MaxY && a.MaxY + expansion >= b.MinY &&
                   a.MinZ - expansion <= b.MaxZ && a.MaxZ + expansion >= b.MinZ;
        }

        private static ClosestResult FindClosest(PhysicalBar a, PhysicalBar b)
        {
            var best = new ClosestResult { Distance = double.MaxValue };
            foreach (Segment3 sa in a.Segments)
            {
                foreach (Segment3 sb in b.Segments)
                {
                    ClosestResult current = SegmentClosest(sa, sb);
                    if (current.Distance < best.Distance)
                    {
                        best = current;
                        if (best.Distance <= Epsilon) return best;
                    }
                }
            }
            return best;
        }

        private static ClosestResult SegmentClosest(Segment3 first, Segment3 second)
        {
            XYZ p1 = first.A;
            XYZ q1 = first.B;
            XYZ p2 = second.A;
            XYZ q2 = second.B;
            XYZ d1 = q1 - p1;
            XYZ d2 = q2 - p2;
            XYZ r = p1 - p2;
            double a = d1.DotProduct(d1);
            double e = d2.DotProduct(d2);
            double f = d2.DotProduct(r);
            double s;
            double t;

            if (a <= Epsilon && e <= Epsilon)
            {
                s = t = 0;
            }
            else if (a <= Epsilon)
            {
                s = 0;
                t = Clamp01(f / e);
            }
            else
            {
                double c = d1.DotProduct(r);
                if (e <= Epsilon)
                {
                    t = 0;
                    s = Clamp01(-c / a);
                }
                else
                {
                    double b = d1.DotProduct(d2);
                    double denom = a * e - b * b;
                    s = Math.Abs(denom) > Epsilon ? Clamp01((b * f - c * e) / denom) : 0;
                    t = (b * s + f) / e;
                    if (t < 0)
                    {
                        t = 0;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1)
                    {
                        t = 1;
                        s = Clamp01((b - c) / a);
                    }
                }
            }

            XYZ c1 = p1 + d1 * s;
            XYZ c2 = p2 + d2 * t;
            return new ClosestResult
            {
                Valid = true,
                Distance = (c1 - c2).GetLength(),
                PointA = c1,
                PointB = c2,
                Approximate = first.Approximate || second.Approximate
            };
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
        private static double ToInternalMm(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        private static double ToMm(double internalValue) => UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);
        private static RebarClearancePoint ToPoint(XYZ point) => new RebarClearancePoint
        {
            Xmm = Math.Round(ToMm(point.X), 3),
            Ymm = Math.Round(ToMm(point.Y), 3),
            Zmm = Math.Round(ToMm(point.Z), 3)
        };

        private static RebarClearanceReport Failure(RebarClearanceRequest request, string error) =>
            new RebarClearanceReport
            {
                Success = false,
                ChangedModel = false,
                MinimumClearanceMm = request.MinimumClearanceMm,
                IncludeSameElement = request.IncludeSameElement,
                RequestedRebarCount = request.RebarIds?.Count ?? 0,
                Error = error
            };

        private static RebarClearanceReport WithError(RebarClearanceReport report, string error)
        {
            report.Success = false;
            report.ChangedModel = false;
            report.Error = error;
            return report;
        }

        private sealed class PhysicalBar
        {
            internal PhysicalBar(long rebarId, string uniqueId, int position, double radius, List<Segment3> segments, bool approximate)
            {
                RebarId = rebarId;
                UniqueId = uniqueId;
                Position = position;
                Radius = radius;
                Segments = segments;
                HasApproximateGeometry = approximate || segments.Any(segment => segment.Approximate);
                Box = Box3.FromSegments(segments);
            }
            internal long RebarId { get; }
            internal string UniqueId { get; }
            internal int Position { get; }
            internal double Radius { get; }
            internal List<Segment3> Segments { get; }
            internal bool HasApproximateGeometry { get; }
            internal Box3 Box { get; }
        }

        private sealed class BarPair
        {
            internal BarPair(PhysicalBar a, PhysicalBar b) { A = a; B = b; }
            internal PhysicalBar A { get; }
            internal PhysicalBar B { get; }
        }

        private sealed class Segment3
        {
            internal Segment3(XYZ a, XYZ b, bool approximate) { A = a; B = b; Approximate = approximate; }
            internal XYZ A { get; }
            internal XYZ B { get; }
            internal bool Approximate { get; }
        }

        private sealed class Box3
        {
            internal double MinX { get; private set; }
            internal double MinY { get; private set; }
            internal double MinZ { get; private set; }
            internal double MaxX { get; private set; }
            internal double MaxY { get; private set; }
            internal double MaxZ { get; private set; }

            internal static Box3 FromSegments(IEnumerable<Segment3> segments)
            {
                var points = segments.SelectMany(segment => new[] { segment.A, segment.B }).ToList();
                return new Box3
                {
                    MinX = points.Min(p => p.X), MinY = points.Min(p => p.Y), MinZ = points.Min(p => p.Z),
                    MaxX = points.Max(p => p.X), MaxY = points.Max(p => p.Y), MaxZ = points.Max(p => p.Z)
                };
            }
        }

        private sealed class ClosestResult
        {
            internal bool Valid { get; set; }
            internal double Distance { get; set; }
            internal XYZ PointA { get; set; } = new XYZ(0, 0, 0);
            internal XYZ PointB { get; set; } = new XYZ(0, 0, 0);
            internal bool Approximate { get; set; }
        }
    }
}
