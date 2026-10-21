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
    /// R02: planar host face + host cover + Shape Driven ToCover constraint.
    /// Scope is deliberately narrow: planar exposed host faces, straight Standard bars,
    /// Single / NumberWithSpacing layouts. Free Form, Varying, hooks, couplers and collision
    /// handling remain out of scope.
    /// </summary>
    public static class RebarFaceDrivenCreateService
    {
        private const double MinLineLengthMm = 1.0;
        private const int MaxBarPositions = 1002;
        private const int MaxFaces = 100;

        public static RebarHostFaceInspectionResult InspectHostFaces(Document doc, long hostId)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var result = new RebarHostFaceInspectionResult { HostId = hostId };
            Element? host = doc.GetElement(ElementIdExtensions.CreateId(hostId));
            if (host == null)
                return FaceInspectionFailure(result, $"找不到宿主 ElementId={hostId}。");
            if (!RebarHostData.IsValidHost(host))
                return FaceInspectionFailure(result, $"图元 {hostId} 不是有效的 Rebar Host。");

            result.HostName = host.Name;
            Options options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            RebarHostData? hostData = null;
            try
            {
                hostData = RebarHostData.GetRebarHostData(host);
                if (hostData == null)
                    return FaceInspectionFailure(result, "无法取得 RebarHostData。");

                GeometryElement geometry = host.get_Geometry(options);
                foreach (Face face in EnumerateFaces(geometry).Take(MaxFaces))
                {
                    try
                    {
                        result.FaceCount++;
                        var info = BuildFaceInfo(doc, host, hostData, face);
                        result.Faces.Add(info);
                        if (info.IsPlanar && info.IsExposed && string.IsNullOrWhiteSpace(info.Error))
                            result.ExposedPlanarFaceCount++;
                    }
                    finally
                    {
                        try { face.Dispose(); } catch { }
                    }
                }

                if (result.FaceCount >= MaxFaces)
                    result.Warnings.Add($"宿主面只记录前 {MaxFaces} 个；复杂宿主可能被截断。");
                if (result.ExposedPlanarFaceCount == 0)
                    result.Warnings.Add("未找到可用于 R02 的 exposed planar face；R02 暂不支持曲面或 concealed face。");

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                return FaceInspectionFailure(result, ex.Message);
            }
            finally
            {
                hostData?.Dispose();
            }
        }

        public static FaceDrivenRebarCreateResult Execute(
            UIApplication uiapp,
            FaceDrivenRebarCreateRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
                return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            FaceDrivenPlan? plan;
            FaceDrivenRebarCreateResult result = ValidateAndBuildPlan(doc, request, out plan);
            if (!result.Success || request.DryRun || plan == null)
                return result;

            Transaction? transaction = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R02 按面保护层创建钢筋");
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
                    plan.Face.FaceNormal,
                    curves,
                    RebarHookOrientation.Left,
                    RebarHookOrientation.Left,
                    useExistingShapeIfPossible: true,
                    createNewShape: true);

                if (rebar == null)
                    throw new InvalidOperationException("Rebar.CreateFromCurves 返回空对象。");

                ApplyLayout(rebar, request);
                doc.Regenerate();

                ApplyPreferredToCoverConstraint(rebar, plan.FaceReference, result);
                if (!result.PreferredToCoverApplied)
                    throw new InvalidOperationException("未能把 Rebar Plane handle 约束到所选宿主面的 ToCover。R02 为避免生成伪约束钢筋，已回滚。 ");

                doc.Regenerate();
                result.ActualCenterlineOffsetMm = ReadActualCenterlineOffsetMm(rebar, plan.Face);
                if (result.ExpectedCenterlineOffsetMm.HasValue && result.ActualCenterlineOffsetMm.HasValue)
                {
                    double diff = Math.Abs(result.ExpectedCenterlineOffsetMm.Value - result.ActualCenterlineOffsetMm.Value);
                    if (diff > 2.0)
                        result.Warnings.Add($"ToCover 后中心线距宿主面与 Cover+半径相差 {diff:0.###} mm；R02 Runtime 需要核对 Revit 的实际 Cover 语义。 ");
                }

                TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                    transaction,
                    "钢筋创建",
                    "R02 按面保护层 Shape Driven 钢筋已创建。",
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
                result.Message = "R02 按面保护层钢筋创建、ToCover 首选约束及事务提交成功。";
                result.Readback = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
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
                transaction?.Dispose();
                plan?.Dispose();
            }
        }

        private static FaceDrivenRebarCreateResult ValidateAndBuildPlan(
            Document doc,
            FaceDrivenRebarCreateRequest request,
            out FaceDrivenPlan? plan)
        {
            plan = null;
            string layout = NormalizeLayoutMode(request.LayoutMode);
            var result = new FaceDrivenRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                FaceStableReference = request.FaceStableReference ?? string.Empty,
                LayoutMode = layout,
                NumberOfBarPositions = layout == "NumberWithSpacing" ? request.NumberOfBarPositions : 1,
                SpacingMm = layout == "NumberWithSpacing" ? request.SpacingMm : null
            };

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            if (host == null)
                return WithError(result, $"找不到宿主 ElementId={request.HostId}。");
            if (!RebarHostData.IsValidHost(host))
                return WithError(result, $"图元 {request.HostId} 不是有效的 Rebar Host。");
            result.HostName = host.Name;

            if (string.IsNullOrWhiteSpace(request.FaceStableReference))
                return WithError(result, "FaceStableReference 不能为空。请通过 UI 选择宿主面，或先调用 inspect_rebar_host_faces 获取 stable reference。 ");

            Reference faceReference;
            try { faceReference = Reference.ParseFromStableRepresentation(doc, request.FaceStableReference); }
            catch (Exception ex) { return WithError(result, "无法解析 FaceStableReference：" + ex.Message); }

            if (faceReference.ElementId.GetIdValue() != request.HostId)
            {
                faceReference.Dispose();
                return WithError(result, "FaceStableReference 不属于指定 HostId。 ");
            }

            GeometryObject geometryObject;
            try { geometryObject = host.GetGeometryObjectFromReference(faceReference); }
            catch (Exception ex)
            {
                faceReference.Dispose();
                return WithError(result, "无法从 Reference 取得宿主面：" + ex.Message);
            }

            if (geometryObject is not PlanarFace face)
            {
                faceReference.Dispose();
                geometryObject?.Dispose();
                return WithError(result, "R02 仅支持 PlanarFace；曲面留给后续 Free Form / Surface 阶段。 ");
            }

            result.FaceType = nameof(PlanarFace);
            result.FaceNormal = ToVector(face.FaceNormal);
            XYZ inward = face.FaceNormal.Negate().Normalize();
            result.InwardNormal = ToVector(inward);

            RebarHostData? hostData = null;
            try
            {
                hostData = RebarHostData.GetRebarHostData(host);
                if (hostData == null)
                    return DisposeAndError(result, face, faceReference, "无法取得 RebarHostData。 ");
                if (!hostData.IsFaceExposed(faceReference))
                    return DisposeAndError(result, face, faceReference, "所选宿主面不是 exposed rebar-cover face。R02 不在 concealed face 上创建。 ");

                RebarCoverType? coverType = hostData.GetCoverType(faceReference);
                if (coverType == null)
                    return DisposeAndError(result, face, faceReference, "所选宿主面没有可读取的 Rebar Cover Type。 ");

                result.CoverTypeId = coverType.Id.GetIdValue();
                result.CoverTypeName = coverType.Name;
                result.CoverDistanceMm = ToMillimeters(coverType.CoverDistance);

                RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
                if (barType == null)
                    return DisposeAndError(result, face, faceReference, "当前项目中没有可用的 RebarBarType。 ");
                result.BarTypeId = barType.Id.GetIdValue();
                result.BarTypeName = barType.Name;

                double barDiameter = ReadBarModelDiameter(barType);
                if (barDiameter <= 0)
                    return DisposeAndError(result, face, faceReference, "无法读取有效的 Rebar model diameter。 ");
                result.BarModelDiameterMm = ToMillimeters(barDiameter);

                XYZ inputStart = ToInternalPoint(request.StartOnFaceMm);
                XYZ inputEnd = ToInternalPoint(request.EndOnFaceMm);
                IntersectionResult? startProjection = face.Project(inputStart);
                IntersectionResult? endProjection = face.Project(inputEnd);
                if (startProjection == null || endProjection == null)
                    return DisposeAndError(result, face, faceReference, "起点或终点无法投影到所选 bounded PlanarFace。 ");

                XYZ projectedStart = startProjection.XYZPoint;
                XYZ projectedEnd = endProjection.XYZPoint;
                double projectedLength = projectedStart.DistanceTo(projectedEnd);
                result.ProjectedLineLengthMm = ToMillimeters(projectedLength);
                if (result.ProjectedLineLengthMm < MinLineLengthMm)
                    return DisposeAndError(result, face, faceReference, $"投影后的钢筋中心线长度必须大于 {MinLineLengthMm:0.###} mm。 ");

                double expectedCenterlineOffset = coverType.CoverDistance + 0.5 * barDiameter;
                result.ExpectedCenterlineOffsetMm = ToMillimeters(expectedCenterlineOffset);
                XYZ centerlineStart = projectedStart + inward * expectedCenterlineOffset;
                XYZ centerlineEnd = projectedEnd + inward * expectedCenterlineOffset;

                if (layout != "Single" && layout != "NumberWithSpacing")
                    return DisposeAndError(result, face, faceReference, "R02 仅支持 LayoutMode=Single 或 NumberWithSpacing。 ");
                if (layout == "NumberWithSpacing")
                {
                    if (request.NumberOfBarPositions < 1 || request.NumberOfBarPositions > MaxBarPositions)
                        return DisposeAndError(result, face, faceReference, $"NumberOfBarPositions 必须在 1..{MaxBarPositions} 之间。 ");
                    if (request.SpacingMm <= 0)
                        return DisposeAndError(result, face, faceReference, "SpacingMm 必须大于 0。 ");
                }

                plan = new FaceDrivenPlan(
                    host,
                    faceReference,
                    face,
                    barType,
                    centerlineStart,
                    centerlineEnd);

                // Ownership of face/reference moves to plan.
                faceReference = null!;
                face = null!;
                result.Success = true;
                result.Message = request.DryRun
                    ? "R02 dryRun：PlanarFace、Host Cover、钢筋直径和预期中心线已解析；未启动 Transaction。"
                    : "R02 面+Cover 创建参数校验通过。";
                result.Warnings.Add("R02 只验证 PlanarFace + Host Cover + RebarPlane ToCover；起终点 handle 仍交给 Revit 默认约束逻辑。 ");
                result.Warnings.Add("R02 尚不验证宿主后续尺寸/Cover 修改的自动跟随；该行为必须由 Runtime 回归证据确认后再进入 R03。 ");
                return result;
            }
            catch (Exception ex)
            {
                return DisposeAndError(result, face, faceReference, ex.Message);
            }
            finally
            {
                hostData?.Dispose();
            }
        }

        private static void ApplyPreferredToCoverConstraint(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            Reference faceReference,
            FaceDrivenRebarCreateResult result)
        {
            using (RebarConstraintsManager manager = rebar.GetRebarConstraintsManager())
            {
                IList<RebarConstrainedHandle> handles = manager.GetAllHandles();
                try
                {
                    RebarConstrainedHandle? planeHandle = handles.FirstOrDefault(handle =>
                    {
                        try { return handle.GetHandleType() == RebarHandleType.RebarPlane; }
                        catch { return false; }
                    });
                    if (planeHandle == null)
                        throw new InvalidOperationException("未找到 RebarPlane constrained handle。 ");

                    IList<RebarConstraint> candidates = manager.GetConstraintCandidatesForHandle(planeHandle, faceReference);
                    try
                    {
                        RebarConstraint? coverCandidate = candidates.FirstOrDefault(candidate =>
                        {
                            try { return candidate.GetConstraintType() == RebarConstraintType.ToCover; }
                            catch { return false; }
                        });
                        if (coverCandidate == null)
                            throw new InvalidOperationException("所选宿主面没有提供 RebarPlane -> ToCover constraint candidate。 ");

                        RebarApiCompatibility.SetPreferredConstraint(manager, planeHandle, coverCandidate);
                        RebarConstraint? preferred = null;
                        try
                        {
                            preferred = manager.GetPreferredConstraintOnHandle(planeHandle);
                            result.PreferredConstraintType = preferred?.GetConstraintType().ToString() ?? string.Empty;
                            result.PreferredToCoverApplied = preferred != null &&
                                                             preferred.GetConstraintType() == RebarConstraintType.ToCover;
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

        private static double? ReadActualCenterlineOffsetMm(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            PlanarFace face)
        {
            try
            {
                IList<Curve> curves = rebar.GetCenterlineCurves(
                    false,
                    false, // Revit 2026+ renamed this argument suppressHooksAndCranks.
                    false,
                    MultiplanarOption.IncludeAllMultiplanarCurves,
                    0);
                Curve? first = curves.FirstOrDefault();
                if (first == null) return null;
                XYZ point = first.GetEndPoint(0);
                double signed = (point - face.Origin).DotProduct(face.FaceNormal.Normalize());
                return Math.Round(Math.Abs(ToMillimeters(signed)), 3);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<Face> EnumerateFaces(GeometryElement geometry)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-9)
                {
                    foreach (Face face in solid.Faces)
                        yield return face;
                    continue;
                }

                if (obj is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry;
                    try { instanceGeometry = instance.GetInstanceGeometry(); }
                    catch { continue; }
                    foreach (Face face in EnumerateFaces(instanceGeometry))
                        yield return face;
                }
            }
        }

        private static RebarHostFaceInfo BuildFaceInfo(
            Document doc,
            Element host,
            RebarHostData hostData,
            Face face)
        {
            var info = new RebarHostFaceInfo
            {
                HostId = host.Id.GetIdValue(),
                HostName = host.Name,
                HostCategory = host.Category?.Name ?? string.Empty,
                FaceType = face.GetType().Name,
                IsPlanar = face is PlanarFace,
                AreaMm2 = ToSquareMillimeters(face.Area)
            };

            try
            {
                Reference reference = face.Reference;
                if (reference == null)
                {
                    info.Error = "Face Reference 为空。";
                    return info;
                }

                info.StableReference = reference.ConvertToStableRepresentation(doc);
                info.IsExposed = hostData.IsFaceExposed(reference);
                if (face is PlanarFace planar)
                {
                    info.OriginMm = ToPoint(planar.Origin);
                    info.Normal = ToVector(planar.FaceNormal);
                }

                if (info.IsExposed)
                {
                    RebarCoverType? cover = hostData.GetCoverType(reference);
                    if (cover != null)
                    {
                        info.CoverTypeId = cover.Id.GetIdValue();
                        info.CoverTypeName = cover.Name;
                        info.CoverDistanceMm = ToMillimeters(cover.CoverDistance);
                    }
                }
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }

            return info;
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

        private static void ApplyLayout(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            FaceDrivenRebarCreateRequest request)
        {
            string mode = NormalizeLayoutMode(request.LayoutMode);
            using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
            {
                if (mode == "Single")
                {
                    accessor.SetLayoutAsSingle();
                    return;
                }

                double spacing = UnitUtils.ConvertToInternalUnits(request.SpacingMm, UnitTypeId.Millimeters);
                accessor.SetLayoutAsNumberWithSpacing(
                    request.NumberOfBarPositions,
                    spacing,
                    request.BarsOnNormalSide,
                    request.IncludeFirstBar,
                    request.IncludeLastBar);
            }
        }

        private static XYZ ToInternalPoint(RebarPointMm point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            return new XYZ(
                UnitUtils.ConvertToInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Y, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Z, UnitTypeId.Millimeters));
        }

        private static RebarPointMm ToPoint(XYZ point)
        {
            return new RebarPointMm
            {
                X = ToMillimeters(point.X),
                Y = ToMillimeters(point.Y),
                Z = ToMillimeters(point.Z)
            };
        }

        private static RebarVectorInput ToVector(XYZ vector)
        {
            return new RebarVectorInput { X = vector.X, Y = vector.Y, Z = vector.Z };
        }

        private static string NormalizeLayoutMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();
            if (mode.Equals("NumberWithSpacing", StringComparison.OrdinalIgnoreCase))
                return "NumberWithSpacing";
            return mode.Equals("Single", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(mode)
                ? "Single"
                : mode;
        }

        private static double ToMillimeters(double internalValue)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), 3);
        }

        private static double ToSquareMillimeters(double internalArea)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalArea, UnitTypeId.SquareMillimeters), 3);
        }

        private static FaceDrivenRebarCreateResult Failure(FaceDrivenRebarCreateRequest request, string error)
        {
            return new FaceDrivenRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                FaceStableReference = request.FaceStableReference ?? string.Empty,
                LayoutMode = NormalizeLayoutMode(request.LayoutMode),
                Error = error
            };
        }

        private static FaceDrivenRebarCreateResult WithError(FaceDrivenRebarCreateResult result, string error)
        {
            result.Success = false;
            result.Error = error;
            return result;
        }

        private static FaceDrivenRebarCreateResult DisposeAndError(
            FaceDrivenRebarCreateResult result,
            PlanarFace? face,
            Reference? reference,
            string error)
        {
            try { face?.Dispose(); } catch { }
            try { reference?.Dispose(); } catch { }
            return WithError(result, error);
        }

        private static RebarHostFaceInspectionResult FaceInspectionFailure(
            RebarHostFaceInspectionResult result,
            string error)
        {
            result.Success = false;
            result.Error = error;
            return result;
        }

        private sealed class FaceDrivenPlan : IDisposable
        {
            public FaceDrivenPlan(
                Element host,
                Reference faceReference,
                PlanarFace face,
                RebarBarType barType,
                XYZ centerlineStart,
                XYZ centerlineEnd)
            {
                Host = host;
                FaceReference = faceReference;
                Face = face;
                BarType = barType;
                CenterlineStart = centerlineStart;
                CenterlineEnd = centerlineEnd;
            }

            public Element Host { get; }
            public Reference FaceReference { get; }
            public PlanarFace Face { get; }
            public RebarBarType BarType { get; }
            public XYZ CenterlineStart { get; }
            public XYZ CenterlineEnd { get; }

            public void Dispose()
            {
                try { Face.Dispose(); } catch { }
                try { FaceReference.Dispose(); } catch { }
            }
        }
    }
}
