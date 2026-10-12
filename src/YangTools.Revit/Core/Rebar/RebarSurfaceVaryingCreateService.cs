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
    /// R03: Shape Driven surface-distributed straight rebar set with explicit cover/end constraints
    /// and optional varying-length computation.
    ///
    /// Key distinction from R02:
    /// - R02 used RebarPlane -> ToCover, which is correct for proving cover-follow on a single bar
    ///   but makes a Shape Driven set distribute normal to that rebar plane.
    /// - R03 rotates the rebar primary plane so its normal lies in the host face. This makes the
    ///   set distribution direction lie along the host surface. The straight segment Edge handle
    ///   then controls depth and is constrained ToCover, while Start/End handles are constrained
    ///   to explicit boundary faces.
    /// </summary>
    public static class RebarSurfaceVaryingCreateService
    {
        private const double MinLineLengthMm = 1.0;
        private const double ParallelTolerance = 1e-8;
        private const int MaxBarPositions = 1002;

        public static SurfaceVaryingRebarCreateResult Execute(
            UIApplication uiapp,
            SurfaceVaryingRebarCreateRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
                return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            SurfaceVaryingPlan? plan;
            SurfaceVaryingRebarCreateResult result = ValidateAndBuildPlan(doc, request, out plan);
            if (!result.Success || request.DryRun || plan == null)
                return result;

            using (plan)
            {
                Transaction? transaction = null;
                try
                {
                    transaction = new Transaction(doc, "YangTools R03 表面分布 / Varying 钢筋");
                    TransactionStatus startStatus = transaction.Start();
                    if (startStatus != TransactionStatus.Started)
                        return WithError(result, $"无法启动 Revit 事务：{startStatus}");

                    Line line = Line.CreateBound(plan.CenterlineStart, plan.CenterlineEnd);
                    var curves = new List<Curve> { line };
                    Autodesk.Revit.DB.Structure.Rebar rebar = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                        doc,
                        RebarStyle.Standard,
                        plan.BarType,
                        null,
                        null,
                        plan.Host,
                        plan.DistributionNormal,
                        curves,
                        RebarHookOrientation.Left,
                        RebarHookOrientation.Left,
                        useExistingShapeIfPossible: true,
                        createNewShape: true);

                    if (rebar == null)
                        throw new InvalidOperationException("Rebar.CreateFromCurves 返回空对象。");

                    ApplyLayout(rebar, request);
                    doc.Regenerate();

                    ApplyToCoverConstraint(
                        rebar,
                        RebarHandleType.Edge,
                        plan.CoverFaceReference,
                        result,
                        "Edge");
                    ApplyToCoverConstraint(
                        rebar,
                        RebarHandleType.StartOfBar,
                        plan.StartBoundaryFaceReference,
                        result,
                        "Start");
                    ApplyToCoverConstraint(
                        rebar,
                        RebarHandleType.EndOfBar,
                        plan.EndBoundaryFaceReference,
                        result,
                        "End");

                    if (!result.EdgeToCoverApplied || !result.StartToCoverApplied || !result.EndToCoverApplied)
                        throw new InvalidOperationException(
                            "R03 未能建立完整的 Edge/Start/End -> ToCover 约束；为避免生成伪自适应钢筋，事务已回滚。");

                    RebarApiCompatibility.SetShapeDrivenVaryingEnabled(rebar, request.EnableVarying);
                    doc.Regenerate();

                    result.VaryingApiMode = RebarApiCompatibility.ReadVaryingMode(rebar);
                    result.DistributionType = ReadOptionalMember(rebar, "DistributionType")?.ToString() ?? string.Empty;
                    result.CanHaveVaryingLengthBars = ReadOptionalBoolMember(rebar, "CanHaveVaryingLengthBars");
                    result.HasVariableLengthBars = ReadOptionalBoolMember(rebar, "HasVariableLengthBars");
                    result.ActualCoverCenterlineOffsetMm = ReadActualCenterlineOffsetMm(rebar, plan.CoverFace);

                    TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                        transaction,
                        "钢筋创建",
                        "R03 表面分布 / Varying Shape Driven 钢筋已创建。",
                        uidoc: null,
                        showDialog: false);

                    result.TransactionStatus = commitStatus.ToString();
                    if (commitStatus != TransactionStatus.Committed)
                    {
                        result.Success = false;
                        result.Error = $"Revit 事务未成功提交：{commitStatus}";
                        return result;
                    }

                    result.CreatedRebarId = rebar.Id.GetIdValue();
                    result.ChangedModel = true;
                    result.Message = request.EnableVarying
                        ? "R03 表面分布 Shape Driven 钢筋已创建，并启用约束驱动 Varying。"
                        : "R03 表面分布 Shape Driven 钢筋已创建（Uniform 控制组）。";
                    result.Readback = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });

                    if (request.EnableVarying && result.HasVariableLengthBars == false)
                    {
                        result.Warnings.Add(
                            "已启用约束驱动 Varying，但当前宿主几何可能仍产生等长钢筋；这在平行边界上是合法结果。Runtime 应使用斜/偏边界夹具确认真正变长。 ");
                    }
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
                    return result;
                }
                finally
                {
                    try { transaction?.Dispose(); } catch { }
                }
            }
        }

        private static SurfaceVaryingRebarCreateResult ValidateAndBuildPlan(
            Document doc,
            SurfaceVaryingRebarCreateRequest request,
            out SurfaceVaryingPlan? plan)
        {
            plan = null;
            var result = new SurfaceVaryingRebarCreateResult
            {
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                CoverFaceStableReference = request.CoverFaceStableReference ?? string.Empty,
                StartBoundaryFaceStableReference = request.StartBoundaryFaceStableReference ?? string.Empty,
                EndBoundaryFaceStableReference = request.EndBoundaryFaceStableReference ?? string.Empty,
                NumberOfBarPositions = request.NumberOfBarPositions,
                SpacingMm = request.SpacingMm,
                EnableVarying = request.EnableVarying
            };

            if (request.HostId <= 0)
                return WithError(result, "HostId 必须大于 0。");
            if (string.IsNullOrWhiteSpace(request.CoverFaceStableReference))
                return WithError(result, "CoverFaceStableReference 不能为空。");
            if (string.IsNullOrWhiteSpace(request.StartBoundaryFaceStableReference))
                return WithError(result, "StartBoundaryFaceStableReference 不能为空。");
            if (string.IsNullOrWhiteSpace(request.EndBoundaryFaceStableReference))
                return WithError(result, "EndBoundaryFaceStableReference 不能为空。");
            if (request.NumberOfBarPositions < 2 || request.NumberOfBarPositions > MaxBarPositions)
                return WithError(result, $"R03 NumberOfBarPositions 必须在 2..{MaxBarPositions} 之间。");
            if (request.SpacingMm <= 0)
                return WithError(result, "SpacingMm 必须大于 0。");

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            if (host == null)
                return WithError(result, $"找不到宿主 ElementId={request.HostId}。");
            if (!RebarHostData.IsValidHost(host))
                return WithError(result, $"图元 {request.HostId} 不是有效的 Rebar Host。");
            result.HostName = host.Name;

            Reference? coverRef = null;
            Reference? startRef = null;
            Reference? endRef = null;
            PlanarFace? coverFace = null;
            PlanarFace? startFace = null;
            PlanarFace? endFace = null;
            RebarHostData? hostData = null;

            try
            {
                coverRef = ParseFaceReference(doc, request.CoverFaceStableReference, host.Id, "Cover");
                startRef = ParseFaceReference(doc, request.StartBoundaryFaceStableReference, host.Id, "StartBoundary");
                endRef = ParseFaceReference(doc, request.EndBoundaryFaceStableReference, host.Id, "EndBoundary");

                coverFace = ResolvePlanarFace(doc, coverRef, "Cover");
                startFace = ResolvePlanarFace(doc, startRef, "StartBoundary");
                endFace = ResolvePlanarFace(doc, endRef, "EndBoundary");

                hostData = RebarHostData.GetRebarHostData(host);
                if (hostData == null)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "无法取得 RebarHostData。");

                EnsureExposed(hostData, coverRef, "Cover");
                EnsureExposed(hostData, startRef, "StartBoundary");
                EnsureExposed(hostData, endRef, "EndBoundary");

                RebarCoverType coverType = GetCoverType(hostData, coverRef, "Cover");
                RebarCoverType startCoverType = GetCoverType(hostData, startRef, "StartBoundary");
                RebarCoverType endCoverType = GetCoverType(hostData, endRef, "EndBoundary");

                result.CoverDistanceMm = ToMillimeters(coverType.CoverDistance);
                result.StartBoundaryCoverDistanceMm = ToMillimeters(startCoverType.CoverDistance);
                result.EndBoundaryCoverDistanceMm = ToMillimeters(endCoverType.CoverDistance);

                RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
                if (barType == null)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "当前项目中没有可用的 RebarBarType。");

                double barDiameter = ReadBarModelDiameter(barType);
                if (barDiameter <= 0)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "无法读取有效的 Rebar model diameter。");
                double radius = 0.5 * barDiameter;
                result.BarModelDiameterMm = ToMillimeters(barDiameter);

                XYZ seedStart = ToInternalPoint(request.SeedStartOnCoverFaceMm);
                XYZ seedEnd = ToInternalPoint(request.SeedEndOnCoverFaceMm);
                IntersectionResult? startProjection = coverFace.Project(seedStart);
                IntersectionResult? endProjection = coverFace.Project(seedEnd);
                if (startProjection == null || endProjection == null)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "Seed 起点或终点无法投影到 Cover bounded PlanarFace。");

                XYZ projectedStart = startProjection.XYZPoint;
                XYZ projectedEnd = endProjection.XYZPoint;
                XYZ seedVector = projectedEnd - projectedStart;
                if (ToMillimeters(seedVector.GetLength()) < MinLineLengthMm)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, $"Seed 方向长度必须大于 {MinLineLengthMm:0.###} mm。");

                XYZ seedDirection = seedVector.Normalize();
                XYZ coverNormal = coverFace.FaceNormal.Normalize();
                XYZ coverInward = coverNormal.Negate();
                double coverOffset = coverType.CoverDistance + radius;
                XYZ basePoint = projectedStart + coverInward * coverOffset;

                XYZ startInward = startFace.FaceNormal.Normalize().Negate();
                XYZ endInward = endFace.FaceNormal.Normalize().Negate();
                XYZ startPlanePoint = startFace.Origin + startInward * (startCoverType.CoverDistance + radius);
                XYZ endPlanePoint = endFace.Origin + endInward * (endCoverType.CoverDistance + radius);

                XYZ centerlineStart = IntersectLineWithPlane(basePoint, seedDirection, startPlanePoint, startFace.FaceNormal, "StartBoundary");
                XYZ centerlineEnd = IntersectLineWithPlane(basePoint, seedDirection, endPlanePoint, endFace.FaceNormal, "EndBoundary");

                XYZ barVector = centerlineEnd - centerlineStart;
                double barLength = barVector.GetLength();
                if (ToMillimeters(barLength) < MinLineLengthMm)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "边界 Cover 面求交后钢筋长度无效。");
                XYZ barDirection = barVector.Normalize();

                // For a straight bar the primary plane is ambiguous. Choosing a plane normal that lies
                // in the cover face makes the set expand along that surface. The Edge handle then moves
                // through cover depth and can be constrained ToCover.
                XYZ distributionNormal = barDirection.CrossProduct(coverNormal);
                if (distributionNormal.GetLength() < ParallelTolerance)
                    return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, "无法从钢筋方向与 Cover 法向构造表面内分布方向。");
                distributionNormal = distributionNormal.Normalize();

                result.CoverFaceNormal = ToVector(coverNormal);
                result.BarDirection = ToVector(barDirection);
                result.DistributionNormal = ToVector(distributionNormal);
                result.DistributionNormalDotCoverNormal = Math.Round(distributionNormal.DotProduct(coverNormal), 9);
                result.DistributionNormalDotBarDirection = Math.Round(distributionNormal.DotProduct(barDirection), 9);
                result.ExpectedCoverCenterlineOffsetMm = ToMillimeters(coverOffset);
                result.InitialBarLengthMm = ToMillimeters(barLength);

                plan = new SurfaceVaryingPlan(
                    host,
                    barType,
                    coverRef,
                    startRef,
                    endRef,
                    coverFace,
                    startFace,
                    endFace,
                    centerlineStart,
                    centerlineEnd,
                    distributionNormal);

                coverRef = null;
                startRef = null;
                endRef = null;
                coverFace = null;
                startFace = null;
                endFace = null;

                result.Success = true;
                result.Message = request.DryRun
                    ? "R03 dryRun：三个 exposed PlanarFace、Cover、表面分布方向和边界中心线已解析；未启动 Transaction。"
                    : "R03 表面分布 / Varying 创建参数校验通过。";
                result.Warnings.Add("R03 仅处理同一 Host 的三个 exposed PlanarFace；曲面与 Free Form 不在本阶段。 ");
                result.Warnings.Add("真正的 Varying 取决于边界几何；平行边界即使启用 varying 也可能保持等长。 ");
                return result;
            }
            catch (Exception ex)
            {
                return DisposeAndError(result, coverFace, startFace, endFace, coverRef, startRef, endRef, ex.Message);
            }
            finally
            {
                hostData?.Dispose();
            }
        }

        private static Reference ParseFaceReference(Document doc, string stable, ElementId hostId, string label)
        {
            Reference reference;
            try { reference = Reference.ParseFromStableRepresentation(doc, stable); }
            catch (Exception ex) { throw new InvalidOperationException($"无法解析 {label} FaceStableReference：{ex.Message}"); }

            if (reference.ElementId != hostId)
            {
                reference.Dispose();
                throw new InvalidOperationException($"{label} FaceStableReference 不属于指定宿主 {hostId.GetIdValue()}。");
            }
            return reference;
        }

        private static PlanarFace ResolvePlanarFace(Document doc, Reference reference, string label)
        {
            Element? element = doc.GetElement(reference.ElementId);
            GeometryObject? geometryObject = element?.GetGeometryObjectFromReference(reference);
            if (geometryObject is not PlanarFace face)
                throw new InvalidOperationException($"{label} reference 不是 PlanarFace；R03 不接受曲面。");
            return face;
        }

        private static void EnsureExposed(RebarHostData hostData, Reference reference, string label)
        {
            if (!hostData.IsFaceExposed(reference))
                throw new InvalidOperationException($"{label} 面不是 exposed rebar-cover face。");
        }

        private static RebarCoverType GetCoverType(RebarHostData hostData, Reference reference, string label)
        {
            RebarCoverType? cover = hostData.GetCoverType(reference);
            if (cover == null)
                throw new InvalidOperationException($"{label} 面没有可读取的 Rebar Cover Type。");
            return cover;
        }

        private static XYZ IntersectLineWithPlane(
            XYZ linePoint,
            XYZ lineDirection,
            XYZ planePoint,
            XYZ planeNormal,
            string label)
        {
            XYZ n = planeNormal.Normalize();
            double denominator = lineDirection.DotProduct(n);
            if (Math.Abs(denominator) < ParallelTolerance)
                throw new InvalidOperationException($"Seed 钢筋方向与 {label} Cover 面近似平行，无法确定钢筋端点。");
            double t = (planePoint - linePoint).DotProduct(n) / denominator;
            return linePoint + lineDirection * t;
        }

        private static void ApplyLayout(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            SurfaceVaryingRebarCreateRequest request)
        {
            using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
            {
                double spacing = UnitUtils.ConvertToInternalUnits(request.SpacingMm, UnitTypeId.Millimeters);
                accessor.SetLayoutAsNumberWithSpacing(
                    request.NumberOfBarPositions,
                    spacing,
                    request.BarsOnNormalSide,
                    request.IncludeFirstBar,
                    request.IncludeLastBar);
            }
        }

        private static void ApplyToCoverConstraint(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            RebarHandleType handleType,
            Reference faceReference,
            SurfaceVaryingRebarCreateResult result,
            string slot)
        {
            using (RebarConstraintsManager manager = rebar.GetRebarConstraintsManager())
            {
                IList<RebarConstrainedHandle> handles = manager.GetAllHandles();
                try
                {
                    RebarConstrainedHandle? handle = handles.FirstOrDefault(h =>
                    {
                        try { return h.GetHandleType() == handleType; }
                        catch { return false; }
                    });
                    if (handle == null)
                        throw new InvalidOperationException($"未找到 {handleType} constrained handle。");

                    IList<RebarConstraint> candidates = manager.GetConstraintCandidatesForHandle(handle, faceReference);
                    try
                    {
                        RebarConstraint? coverCandidate = candidates.FirstOrDefault(candidate =>
                        {
                            try { return candidate.GetConstraintType() == RebarConstraintType.ToCover; }
                            catch { return false; }
                        });
                        if (coverCandidate == null)
                            throw new InvalidOperationException($"{handleType} 没有针对指定面的 ToCover candidate。");

                        RebarApiCompatibility.SetPreferredConstraint(manager, handle, coverCandidate);
                        RebarConstraint? preferred = null;
                        try
                        {
                            preferred = manager.GetPreferredConstraintOnHandle(handle);
                            string type = preferred?.GetConstraintType().ToString() ?? string.Empty;
                            bool applied = preferred != null && preferred.GetConstraintType() == RebarConstraintType.ToCover;
                            SetConstraintResult(result, slot, applied, type);
                        }
                        finally
                        {
                            preferred?.Dispose();
                        }
                    }
                    finally
                    {
                        foreach (RebarConstraint candidate in candidates)
                        {
                            try { candidate.Dispose(); } catch { }
                        }
                    }
                }
                finally
                {
                    foreach (RebarConstrainedHandle handle in handles)
                    {
                        try { handle.Dispose(); } catch { }
                    }
                }
            }
        }

        private static void SetConstraintResult(
            SurfaceVaryingRebarCreateResult result,
            string slot,
            bool applied,
            string type)
        {
            if (slot == "Edge")
            {
                result.EdgeToCoverApplied = applied;
                result.EdgeConstraintType = type;
            }
            else if (slot == "Start")
            {
                result.StartToCoverApplied = applied;
                result.StartConstraintType = type;
            }
            else if (slot == "End")
            {
                result.EndToCoverApplied = applied;
                result.EndConstraintType = type;
            }
        }

        private static double? ReadActualCenterlineOffsetMm(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            PlanarFace face)
        {
            try
            {
                IList<Curve> curves = rebar.GetCenterlineCurves(
                    adjustForSelfIntersection: false,
                    suppressHooks: false,
                    suppressBendRadius: false,
                    multiplanarOption: MultiplanarOption.IncludeAllMultiplanarCurves,
                    barPositionIndex: 0);
                Curve? first = curves.FirstOrDefault();
                if (first == null) return null;
                XYZ point = first.GetEndPoint(0);
                double signed = (point - face.Origin).DotProduct(face.FaceNormal.Normalize());
                return Math.Round(Math.Abs(ToMillimeters(signed)), 3);
            }
            catch { return null; }
        }

        private static RebarBarType? ResolveBarType(Document doc, long? requestedId)
        {
            if (requestedId.HasValue)
            {
                Element? element = doc.GetElement(ElementIdExtensions.CreateId(requestedId.Value));
                if (element is RebarBarType requestedType)
                    return requestedType;
            }
            return RebarShapeDrivenCreateService.GetAvailableBarTypes(doc).FirstOrDefault();
        }

        private static double ReadBarModelDiameter(RebarBarType barType)
        {
#if REVIT2022_OR_GREATER
            return barType.BarModelDiameter;
#else
#pragma warning disable CS0618
            return barType.BarDiameter;
#pragma warning restore CS0618
#endif
        }

        private static object? ReadOptionalMember(object instance, string memberName)
        {
            try
            {
                var property = instance.GetType().GetProperty(memberName);
                if (property != null) return property.GetValue(instance, null);
            }
            catch { }
            return null;
        }

        private static bool? ReadOptionalBoolMember(object instance, string memberName)
        {
            object? value = ReadOptionalMember(instance, memberName);
            return value is bool b ? b : null;
        }

        private static XYZ ToInternalPoint(RebarPointMm point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            return new XYZ(
                UnitUtils.ConvertToInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Y, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Z, UnitTypeId.Millimeters));
        }

        private static RebarVectorInput ToVector(XYZ vector)
        {
            return new RebarVectorInput { X = vector.X, Y = vector.Y, Z = vector.Z };
        }

        private static double ToMillimeters(double internalValue)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), 3);
        }

        private static SurfaceVaryingRebarCreateResult Failure(
            SurfaceVaryingRebarCreateRequest request,
            string error)
        {
            return new SurfaceVaryingRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                CoverFaceStableReference = request.CoverFaceStableReference ?? string.Empty,
                StartBoundaryFaceStableReference = request.StartBoundaryFaceStableReference ?? string.Empty,
                EndBoundaryFaceStableReference = request.EndBoundaryFaceStableReference ?? string.Empty,
                Error = error
            };
        }

        private static SurfaceVaryingRebarCreateResult WithError(
            SurfaceVaryingRebarCreateResult result,
            string error)
        {
            result.Success = false;
            result.Error = error;
            return result;
        }

        private static SurfaceVaryingRebarCreateResult DisposeAndError(
            SurfaceVaryingRebarCreateResult result,
            PlanarFace? coverFace,
            PlanarFace? startFace,
            PlanarFace? endFace,
            Reference? coverRef,
            Reference? startRef,
            Reference? endRef,
            string error)
        {
            try { coverFace?.Dispose(); } catch { }
            try { startFace?.Dispose(); } catch { }
            try { endFace?.Dispose(); } catch { }
            try { coverRef?.Dispose(); } catch { }
            try { startRef?.Dispose(); } catch { }
            try { endRef?.Dispose(); } catch { }
            return WithError(result, error);
        }

        private sealed class SurfaceVaryingPlan : IDisposable
        {
            public SurfaceVaryingPlan(
                Element host,
                RebarBarType barType,
                Reference coverFaceReference,
                Reference startBoundaryFaceReference,
                Reference endBoundaryFaceReference,
                PlanarFace coverFace,
                PlanarFace startBoundaryFace,
                PlanarFace endBoundaryFace,
                XYZ centerlineStart,
                XYZ centerlineEnd,
                XYZ distributionNormal)
            {
                Host = host;
                BarType = barType;
                CoverFaceReference = coverFaceReference;
                StartBoundaryFaceReference = startBoundaryFaceReference;
                EndBoundaryFaceReference = endBoundaryFaceReference;
                CoverFace = coverFace;
                StartBoundaryFace = startBoundaryFace;
                EndBoundaryFace = endBoundaryFace;
                CenterlineStart = centerlineStart;
                CenterlineEnd = centerlineEnd;
                DistributionNormal = distributionNormal;
            }

            public Element Host { get; }
            public RebarBarType BarType { get; }
            public Reference CoverFaceReference { get; }
            public Reference StartBoundaryFaceReference { get; }
            public Reference EndBoundaryFaceReference { get; }
            public PlanarFace CoverFace { get; }
            public PlanarFace StartBoundaryFace { get; }
            public PlanarFace EndBoundaryFace { get; }
            public XYZ CenterlineStart { get; }
            public XYZ CenterlineEnd { get; }
            public XYZ DistributionNormal { get; }

            public void Dispose()
            {
                try { CoverFace.Dispose(); } catch { }
                try { StartBoundaryFace.Dispose(); } catch { }
                try { EndBoundaryFace.Dispose(); } catch { }
                try { CoverFaceReference.Dispose(); } catch { }
                try { StartBoundaryFaceReference.Dispose(); } catch { }
                try { EndBoundaryFaceReference.Dispose(); } catch { }
            }
        }
    }
}
