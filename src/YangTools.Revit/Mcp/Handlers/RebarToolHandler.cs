using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;

namespace YangTools.Revit.Mcp
{
    // RC1: Legacy MCP rebar request adapters. Shape, geometry and transaction logic stays in Core.Rebar services.
    internal static class RebarToolHandler
    {
        internal static RevitExecutionResult InspectRebar(UIApplication uiapp, JObject args)
        {
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Failure("当前没有活动 Revit 文档。 ");

            IEnumerable<ElementId> ids;
            JArray? requested = args["elementIds"] as JArray;
            if (requested != null && requested.Count > 0)
            {
                ids = requested
                    .Select(item => item.Value<long?>())
                    .Where(value => value.HasValue)
                    .Take(500)
                    .Select(value => ElementIdExtensions.CreateId(value!.Value))
                    .ToList();
            }
            else
            {
                ids = RebarInspectionService.GetSelectedRebarIds(uidoc);
            }

            List<ElementId> rebarIds = ids
                .Where(id => uidoc.Document.GetElement(id) is Autodesk.Revit.DB.Structure.Rebar)
                .Take(500)
                .ToList();
            if (rebarIds.Count == 0)
                return Failure("没有可检查的 Rebar。请传入 elementIds，或先在 Revit 中选择钢筋。 ");

            RebarInspectionReport report = RebarInspectionService.Inspect(uiapp, rebarIds);
            return Success(JsonConvert.SerializeObject(report, Formatting.None));
        }

        internal static RevitExecutionResult ListRebarTypes(UIApplication uiapp)
        {
            Document? doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null) return Failure("当前没有活动 Revit 文档。 ");

            var array = new JArray();
            foreach (Autodesk.Revit.DB.Structure.RebarBarType barType in
                RebarShapeDrivenCreateService.GetAvailableBarTypes(doc).Take(200))
            {
                array.Add(new JObject
                {
                    ["elementId"] = barType.Id.GetIdValue(),
                    ["name"] = barType.Name
                });
            }
            return Success(array.ToString(Formatting.None));
        }

        internal static RevitExecutionResult ListRebarCouplerTypes(UIApplication uiapp)
        {
            Document? doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null) return Failure("当前没有活动 Revit 文档。 ");
            try
            {
                return Success(JsonConvert.SerializeObject(RebarCouplerService.GetAvailableTypes(doc), Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("list_rebar_coupler_types 执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult CreateRebarCoupler(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new RebarCouplerRequest
                {
                    OperationId = args["operationId"]?.ToString() ?? string.Empty,
                    CouplerTypeId = args["couplerTypeId"]?.Value<long?>(),
                    FirstRebarId = args["firstRebarId"]?.Value<long?>() ?? 0,
                    FirstEnd = args["firstEnd"]?.Value<int?>() ?? -1,
                    ExpectedFirstUniqueId = args["expectedFirstUniqueId"]?.ToString() ?? string.Empty,
                    SecondRebarId = args["secondRebarId"]?.Value<long?>(),
                    SecondEnd = args["secondEnd"]?.Value<int?>() ?? 0,
                    ExpectedSecondUniqueId = args["expectedSecondUniqueId"]?.ToString() ?? string.Empty,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };
                if (request.FirstRebarId <= 0) return Failure("create_rebar_coupler 缺少有效 firstRebarId。 ");
                if (request.FirstEnd != 0 && request.FirstEnd != 1) return Failure("create_rebar_coupler firstEnd 只能是 0 或 1。 ");
                if (request.SecondRebarId.HasValue && request.SecondRebarId.Value > 0 && request.SecondEnd != 0 && request.SecondEnd != 1)
                    return Failure("create_rebar_coupler secondEnd 只能是 0 或 1。 ");
                RebarCouplerResult result = RebarCouplerService.Execute(uiapp, request);
                if (!result.Success) return Failure(JsonConvert.SerializeObject(result, Formatting.None));
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("create_rebar_coupler 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ListRebarHookTypes(UIApplication uiapp)
        {
            Document? doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null) return Failure("当前没有活动 Revit 文档。 ");
            var array = new JArray();
            foreach (Autodesk.Revit.DB.Structure.RebarHookType hookType in
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Autodesk.Revit.DB.Structure.RebarHookType))
                    .Cast<Autodesk.Revit.DB.Structure.RebarHookType>()
                    .OrderBy(x => x.Name)
                    .Take(200))
            {
                array.Add(new JObject
                {
                    ["elementId"] = hookType.Id.GetIdValue(),
                    ["name"] = hookType.Name,
                    ["hookAngleDegrees"] = Math.Round(hookType.HookAngle * 180.0 / Math.PI, 3)
                });
            }
            return Success(array.ToString(Formatting.None));
        }

        internal static RevitExecutionResult InspectRebarHostFaces(UIApplication uiapp, JObject args)
        {
            try
            {
                Document? doc = uiapp.ActiveUIDocument?.Document;
                if (doc == null) return Failure("当前没有活动 Revit 文档。 ");
                long hostId = args["hostId"]?.Value<long?>() ?? 0;
                if (hostId <= 0) return Failure("inspect_rebar_host_faces 缺少有效 hostId。 ");

                RebarHostFaceInspectionResult result = RebarFaceDrivenCreateService.InspectHostFaces(doc, hostId);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("inspect_rebar_host_faces 执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult CreateShapeRebarOnFace(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new FaceDrivenRebarCreateRequest
                {
                    HostId = args["hostId"]?.Value<long?>() ?? 0,
                    FaceStableReference = args["faceStableReference"]?.ToString() ?? string.Empty,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    StartOnFaceMm = ReadPointMm(args["startOnFaceMm"]),
                    EndOnFaceMm = ReadPointMm(args["endOnFaceMm"]),
                    LayoutMode = args["layoutMode"]?.ToString() ?? "Single",
                    NumberOfBarPositions = args["numberOfBarPositions"]?.Value<int?>() ?? 1,
                    SpacingMm = args["spacingMm"]?.Value<double?>() ?? 200.0,
                    BarsOnNormalSide = args["barsOnNormalSide"]?.Value<bool?>() ?? true,
                    IncludeFirstBar = args["includeFirstBar"]?.Value<bool?>() ?? true,
                    IncludeLastBar = args["includeLastBar"]?.Value<bool?>() ?? true,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };

                if (request.HostId <= 0)
                    return Failure("create_shape_rebar_on_face 缺少有效 hostId。 ");
                if (string.IsNullOrWhiteSpace(request.FaceStableReference))
                    return Failure("create_shape_rebar_on_face 缺少 faceStableReference。 ");

                FaceDrivenRebarCreateResult result = RebarFaceDrivenCreateService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("create_shape_rebar_on_face 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult CreateShapeRebarVaryingOnFace(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new SurfaceVaryingRebarCreateRequest
                {
                    HostId = args["hostId"]?.Value<long?>() ?? 0,
                    CoverFaceStableReference = args["coverFaceStableReference"]?.ToString() ?? string.Empty,
                    StartBoundaryFaceStableReference = args["startBoundaryFaceStableReference"]?.ToString() ?? string.Empty,
                    EndBoundaryFaceStableReference = args["endBoundaryFaceStableReference"]?.ToString() ?? string.Empty,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    SeedStartOnCoverFaceMm = ReadPointMm(args["seedStartOnCoverFaceMm"]),
                    SeedEndOnCoverFaceMm = ReadPointMm(args["seedEndOnCoverFaceMm"]),
                    NumberOfBarPositions = args["numberOfBarPositions"]?.Value<int?>() ?? 5,
                    SpacingMm = args["spacingMm"]?.Value<double?>() ?? 200.0,
                    BarsOnNormalSide = args["barsOnNormalSide"]?.Value<bool?>() ?? true,
                    IncludeFirstBar = args["includeFirstBar"]?.Value<bool?>() ?? true,
                    IncludeLastBar = args["includeLastBar"]?.Value<bool?>() ?? true,
                    EnableVarying = args["enableVarying"]?.Value<bool?>() ?? true,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };

                if (request.HostId <= 0)
                    return Failure("create_shape_rebar_varying_on_face 缺少有效 hostId。 ");
                if (string.IsNullOrWhiteSpace(request.CoverFaceStableReference) ||
                    string.IsNullOrWhiteSpace(request.StartBoundaryFaceStableReference) ||
                    string.IsNullOrWhiteSpace(request.EndBoundaryFaceStableReference))
                    return Failure("create_shape_rebar_varying_on_face 缺少 cover/start/end face stable reference。 ");

                SurfaceVaryingRebarCreateResult result = RebarSurfaceVaryingCreateService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("create_shape_rebar_varying_on_face 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult RebindShapeRebarConstraints(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new ShapeDrivenConstraintRebindRequest
                {
                    RebarId = args["rebarId"]?.Value<long?>() ?? 0,
                    ExpectedUniqueId = args["expectedUniqueId"]?.ToString() ?? string.Empty,
                    EdgeFaceStableReference = args["edgeFaceStableReference"]?.ToString() ?? string.Empty,
                    StartFaceStableReference = args["startFaceStableReference"]?.ToString() ?? string.Empty,
                    EndFaceStableReference = args["endFaceStableReference"]?.ToString() ?? string.Empty,
                    RequirePlanarFace = args["requirePlanarFace"]?.Value<bool?>() ?? true,
                    RequireExposedFace = args["requireExposedFace"]?.Value<bool?>() ?? true,
                    PreserveUnretargetedConstraints = args["preserveUnretargetedConstraints"]?.Value<bool?>() ?? true,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };

                if (request.RebarId <= 0)
                    return Failure("rebind_shape_rebar_constraints 缺少有效 rebarId。 ");
                if (string.IsNullOrWhiteSpace(request.EdgeFaceStableReference) &&
                    string.IsNullOrWhiteSpace(request.StartFaceStableReference) &&
                    string.IsNullOrWhiteSpace(request.EndFaceStableReference))
                    return Failure("rebind_shape_rebar_constraints 至少需要一个目标 face stable reference。 ");

                ShapeDrivenConstraintRebindResult result =
                    RebarConstraintRebindService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("rebind_shape_rebar_constraints 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ConstrainRebarToRebar(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new RebarToRebarConstraintRequest
                {
                    SourceRebarId = args["sourceRebarId"]?.Value<long?>() ?? 0,
                    ExpectedSourceUniqueId = args["expectedSourceUniqueId"]?.ToString() ?? string.Empty,
                    TargetRebarId = args["targetRebarId"]?.Value<long?>() ?? 0,
                    ExpectedTargetUniqueId = args["expectedTargetUniqueId"]?.ToString() ?? string.Empty,
                    SourceHandleType = args["sourceHandleType"]?.ToString() ?? "Edge",
                    SourceHandleOrdinal = args["sourceHandleOrdinal"]?.Value<int?>(),
                    TargetKind = args["targetKind"]?.ToString() ?? "Any",
                    TargetIndex = args["targetIndex"]?.Value<int?>(),
                    DistanceMm = args["distanceMm"]?.Value<double?>(),
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };
                if (request.SourceRebarId <= 0 || request.TargetRebarId <= 0)
                    return Failure("constrain_rebar_to_rebar 需要有效 sourceRebarId / targetRebarId。 ");
                RebarToRebarConstraintResult result = RebarRelationshipService.ConstrainToOtherRebar(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("constrain_rebar_to_rebar 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ModifyRebarHooks(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new RebarHookModifyRequest
                {
                    RebarId = args["rebarId"]?.Value<long?>() ?? 0,
                    ExpectedUniqueId = args["expectedUniqueId"]?.ToString() ?? string.Empty,
                    StartHookTypeId = args["startHookTypeId"]?.Value<long?>(),
                    EndHookTypeId = args["endHookTypeId"]?.Value<long?>(),
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };
                if (request.RebarId <= 0) return Failure("modify_rebar_hooks 缺少有效 rebarId。 ");
                RebarHookModifyResult result = RebarHookModifyService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("modify_rebar_hooks 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ModifyShapeRebar(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new ShapeDrivenRebarModifyRequest
                {
                    RebarId = args["rebarId"]?.Value<long?>() ?? 0,
                    ExpectedUniqueId = args["expectedUniqueId"]?.ToString() ?? string.Empty,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    NumberOfBarPositions = args["numberOfBarPositions"]?.Value<int?>(),
                    SpacingMm = args["spacingMm"]?.Value<double?>(),
                    BarsOnNormalSide = args["barsOnNormalSide"]?.Value<bool?>() ?? true,
                    IncludeFirstBar = args["includeFirstBar"]?.Value<bool?>() ?? true,
                    IncludeLastBar = args["includeLastBar"]?.Value<bool?>() ?? true,
                    RequireConstraintPreservation = args["requireConstraintPreservation"]?.Value<bool?>() ?? true,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };

                if (request.RebarId <= 0)
                    return Failure("modify_shape_rebar 缺少有效 rebarId。 ");

                ShapeDrivenRebarModifyResult result = RebarShapeDrivenModifyService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("modify_shape_rebar 参数或执行失败：" + ex.Message);
            }
        }


        internal static RevitExecutionResult CreateFreeFormRebar(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new FreeFormRebarCreateRequest
                {
                    HostId = args["hostId"]?.Value<long?>() ?? 0,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    RebarStyle = args["rebarStyle"]?.ToString() ?? "Standard",
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };
                if (request.HostId <= 0) return Failure("create_free_form_rebar 缺少有效 hostId。");
                if (args["bars"] is not JArray bars || bars.Count == 0)
                    return Failure("create_free_form_rebar 缺少 bars。");
                foreach (JToken barToken in bars)
                {
                    if (barToken is not JArray points) return Failure("bars 每一项必须是点数组。");
                    var path = new FreeFormBarPathInput();
                    foreach (JToken point in points) path.PointsMm.Add(ReadPointMm(point));
                    request.Bars.Add(path);
                }
                FreeFormRebarCreateResult result = RebarFreeFormCreateService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("create_free_form_rebar 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ModifyFreeFormRebar(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new FreeFormRebarModifyRequest
                {
                    RebarId = args["rebarId"]?.Value<long?>() ?? 0,
                    ExpectedUniqueId = args["expectedUniqueId"]?.ToString() ?? string.Empty,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };
                if (request.RebarId <= 0) return Failure("modify_free_form_rebar 缺少有效 rebarId。");
                if (args["bars"] is not JArray bars || bars.Count == 0)
                    return Failure("modify_free_form_rebar 缺少 bars。");
                foreach (JToken barToken in bars)
                {
                    if (barToken is not JArray points) return Failure("bars 每一项必须是点数组。");
                    var path = new FreeFormBarPathInput();
                    foreach (JToken point in points) path.PointsMm.Add(ReadPointMm(point));
                    request.Bars.Add(path);
                }
                FreeFormRebarModifyResult result = RebarFreeFormModifyService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("modify_free_form_rebar 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ProbeGradientRebarCapabilities(UIApplication uiapp)
        {
            try
            {
                RebarGradientCapabilityReport result = RebarGradientCapabilityService.Probe(uiapp);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("probe_gradient_rebar_capabilities 执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult PreviewGradientSpacingPlan(JObject args)
        {
            try
            {
                var request = new RebarGradientSpacingPlanRequest
                {
                    DistributionLengthMm = args["distributionLengthMm"]?.Value<double?>() ?? 0.0,
                    SpacingStepMm = args["spacingStepMm"]?.Value<double?>() ?? 5.0,
                    MinTailRatio = args["minTailRatio"]?.Value<double?>() ?? 0.75,
                    LengthToleranceMm = args["lengthToleranceMm"]?.Value<double?>() ?? 1.0,
                    StationMergeToleranceMm = args["stationMergeToleranceMm"]?.Value<double?>() ?? 0.5,
                    MaxZones = args["maxZones"]?.Value<int?>() ?? 50,
                    MaxPhysicalBars = args["maxPhysicalBars"]?.Value<int?>() ?? 1000
                };
                if (args["zones"] is not JArray zones || zones.Count == 0)
                    return Failure("preview_gradient_spacing_plan 缺少 zones。 ");
                foreach (JToken token in zones)
                {
                    if (token is not JObject zone) return Failure("zones 每一项必须是对象。 ");
                    request.Zones.Add(new RebarGradientSpacingZoneInput
                    {
                        Label = zone["label"]?.ToString() ?? string.Empty,
                        AutoLength = zone["autoLength"]?.Value<bool?>() ?? false,
                        LengthMm = zone["lengthMm"]?.Value<double?>() ?? 0.0,
                        MaxSpacingMm = zone["maxSpacingMm"]?.Value<double?>() ?? 0.0,
                        ManualActualSpacingMm = zone["manualActualSpacingMm"]?.Value<double?>()
                    });
                }
                RebarGradientSpacingPlanResult result = RebarGradientSpacingPlanner.Calculate(request);
                if (!result.Success) return Failure($"{result.ErrorCode}: {result.Error}");
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("preview_gradient_spacing_plan 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ProbeGradientServerCreate(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new RebarGradientServerCreateProbeRequest
                {
                    HostId = args["hostId"]?.Value<long?>() ?? 0,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    Mode = args["mode"]?.ToString() ?? "Surface"
                };
                if (request.HostId <= 0) return Failure("probe_gradient_server_create 缺少有效 hostId。 ");
                RebarGradientServerCreateProbeResult result = RebarGradientServerCreateProbeService.Execute(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("probe_gradient_server_create 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult ExecuteGradientRebarSet(UIApplication uiapp, JObject args, bool forceDryRun)
        {
            try
            {
                var request = new RebarGradientSetRequest
                {
                    OperationId = args["operationId"]?.ToString() ?? string.Empty,
                    StartControlRebarId = args["startControlRebarId"]?.Value<long?>() ?? 0,
                    ExpectedStartControlUniqueId = args["expectedStartControlUniqueId"]?.ToString() ?? string.Empty,
                    EndControlRebarId = args["endControlRebarId"]?.Value<long?>() ?? 0,
                    ExpectedEndControlUniqueId = args["expectedEndControlUniqueId"]?.ToString() ?? string.Empty,
                    SpacingStepMm = args["spacingStepMm"]?.Value<double?>() ?? 5.0,
                    MinTailRatio = args["minTailRatio"]?.Value<double?>() ?? 0.75,
                    LengthToleranceMm = args["lengthToleranceMm"]?.Value<double?>() ?? 1.0,
                    StationMergeToleranceMm = args["stationMergeToleranceMm"]?.Value<double?>() ?? 0.5,
                    PathProjectionToleranceMm = args["pathProjectionToleranceMm"]?.Value<double?>() ?? 10.0,
                    CurveContinuityToleranceMm = args["curveContinuityToleranceMm"]?.Value<double?>() ?? 1.0,
                    GeometryReadbackToleranceMm = args["geometryReadbackToleranceMm"]?.Value<double?>() ?? 1.0,
                    MaxZones = args["maxZones"]?.Value<int?>() ?? 50,
                    MaxPhysicalBars = args["maxPhysicalBars"]?.Value<int?>() ?? 1000,
                    MaxCurvesPerBar = args["maxCurvesPerBar"]?.Value<int?>() ?? 100,
                    ExpectedPlanFingerprint = args["expectedPlanFingerprint"]?.ToString() ?? string.Empty,
                    DryRun = forceDryRun || (args["dryRun"]?.Value<bool?>() ?? true)
                };

                if (request.StartControlRebarId <= 0 || request.EndControlRebarId <= 0)
                    return Failure("GR01 V1 缺少有效 startControlRebarId / endControlRebarId。 ");

                if (args["distributionPath"] is not JObject path)
                    return Failure("GR01 V1 缺少 distributionPath。 ");
                request.DistributionPath = new RebarGradientDistributionPathInput
                {
                    Kind = path["kind"]?.ToString() ?? "ModelLine",
                    ElementId = path["elementId"]?.Value<long?>(),
                    StableReference = path["stableReference"]?.ToString() ?? string.Empty
                };

                if (args["zones"] is not JArray zones || zones.Count == 0)
                    return Failure("GR01 V1 缺少 zones。 ");
                foreach (JToken token in zones)
                {
                    if (token is not JObject zone) return Failure("zones 每一项必须是对象。 ");
                    request.Zones.Add(new RebarGradientSpacingZoneInput
                    {
                        Label = zone["label"]?.ToString() ?? string.Empty,
                        AutoLength = zone["autoLength"]?.Value<bool?>() ?? false,
                        LengthMm = zone["lengthMm"]?.Value<double?>() ?? 0.0,
                        MaxSpacingMm = zone["maxSpacingMm"]?.Value<double?>() ?? 0.0,
                        ManualActualSpacingMm = zone["manualActualSpacingMm"]?.Value<double?>()
                    });
                }

                RebarGradientSetResult result = RebarGradientSetService.Execute(uiapp, request);
                if (!result.Success) return Failure($"{result.ErrorCode}: {result.Error}");
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("GR01 V1 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult AnalyzeRebarClearance(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new RebarClearanceRequest
                {
                    MinimumClearanceMm = args["minimumClearanceMm"]?.Value<double?>() ?? 25.0,
                    IncludeSameElement = args["includeSameElement"]?.Value<bool?>() ?? false,
                    MaxResults = args["maxResults"]?.Value<int?>() ?? 200
                };
                if (args["rebarIds"] is JArray ids)
                {
                    request.RebarIds = ids
                        .Select(item => item.Value<long?>())
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .ToList();
                }

                RebarClearanceReport result = RebarClearanceService.Analyze(uiapp, request);
                if (!result.Success) return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("analyze_rebar_clearance 参数或执行失败：" + ex.Message);
            }
        }

        internal static RevitExecutionResult CreateShapeRebar(UIApplication uiapp, JObject args)
        {
            try
            {
                var request = new ShapeDrivenRebarCreateRequest
                {
                    HostId = args["hostId"]?.Value<long?>() ?? 0,
                    BarTypeId = args["barTypeId"]?.Value<long?>(),
                    StartMm = ReadPointMm(args["startMm"]),
                    EndMm = ReadPointMm(args["endMm"]),
                    Normal = ReadVector(args["normal"], defaultZ: 1.0),
                    LayoutMode = args["layoutMode"]?.ToString() ?? "Single",
                    NumberOfBarPositions = args["numberOfBarPositions"]?.Value<int?>() ?? 1,
                    SpacingMm = args["spacingMm"]?.Value<double?>() ?? 200.0,
                    BarsOnNormalSide = args["barsOnNormalSide"]?.Value<bool?>() ?? true,
                    IncludeFirstBar = args["includeFirstBar"]?.Value<bool?>() ?? true,
                    IncludeLastBar = args["includeLastBar"]?.Value<bool?>() ?? true,
                    DryRun = args["dryRun"]?.Value<bool?>() ?? true
                };

                if (request.HostId <= 0)
                    return Failure("create_shape_rebar 缺少有效 hostId。 ");

                ShapeDrivenRebarCreateResult result = RebarShapeDrivenCreateService.Execute(uiapp, request);
                if (!result.Success)
                    return Failure(result.Error);
                return Success(JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch (Exception ex)
            {
                return Failure("create_shape_rebar 参数或执行失败：" + ex.Message);
            }
        }

        private static RebarPointMm ReadPointMm(JToken? token)
        {
            if (token is JArray array && array.Count >= 3)
            {
                return new RebarPointMm
                {
                    X = array[0]?.Value<double?>() ?? 0.0,
                    Y = array[1]?.Value<double?>() ?? 0.0,
                    Z = array[2]?.Value<double?>() ?? 0.0
                };
            }
            if (token is JObject obj)
            {
                return new RebarPointMm
                {
                    X = obj["x"]?.Value<double?>() ?? obj["X"]?.Value<double?>() ?? 0.0,
                    Y = obj["y"]?.Value<double?>() ?? obj["Y"]?.Value<double?>() ?? 0.0,
                    Z = obj["z"]?.Value<double?>() ?? obj["Z"]?.Value<double?>() ?? 0.0
                };
            }
            throw new ArgumentException("点坐标必须是 [x,y,z] 或 {x,y,z}，单位 mm。");
        }

        private static RebarVectorInput ReadVector(JToken? token, double defaultZ)
        {
            if (token == null || token.Type == JTokenType.Null)
                return new RebarVectorInput { Z = defaultZ };
            if (token is JArray array && array.Count >= 3)
            {
                return new RebarVectorInput
                {
                    X = array[0]?.Value<double?>() ?? 0.0,
                    Y = array[1]?.Value<double?>() ?? 0.0,
                    Z = array[2]?.Value<double?>() ?? 0.0
                };
            }
            if (token is JObject obj)
            {
                return new RebarVectorInput
                {
                    X = obj["x"]?.Value<double?>() ?? obj["X"]?.Value<double?>() ?? 0.0,
                    Y = obj["y"]?.Value<double?>() ?? obj["Y"]?.Value<double?>() ?? 0.0,
                    Z = obj["z"]?.Value<double?>() ?? obj["Z"]?.Value<double?>() ?? defaultZ
                };
            }
            throw new ArgumentException("normal 必须是 [x,y,z] 或 {x,y,z}。");
        }

        private static RevitExecutionResult Success(string result) => new RevitExecutionResult { Success = true, Result = result ?? string.Empty };
        private static RevitExecutionResult Failure(string error) => new RevitExecutionResult { Success = false, Error = error ?? string.Empty };
    }
}
