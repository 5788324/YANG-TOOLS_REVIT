using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class EntityGeneratorEngine
    {
        private const double MinimumEndpointTolerance = 1e-7;

        public static void Execute(UIApplication uiApp, Document doc, Models.EntityGeneratorState state)
        {
            ValidateState(state);

            double endpointTolerance = Math.Max(
                uiApp.Application.ShortCurveTolerance * 0.1,
                MinimumEndpointTolerance);

            DiagnosticLog.WriteInfo("Loft引擎", $"开始提取轮廓；容差={endpointTolerance:G8} ft");

            var orderedProfiles = new List<List<Curve>>();
            var sourceZ = new LoftZBounds();
            var profileIds = new HashSet<string>(StringComparer.Ordinal);

            for (int profileIndex = 0; profileIndex < state.Profiles.Count; profileIndex++)
            {
                FamilyInstance instance = state.Profiles[profileIndex];
                if (instance == null || !instance.IsValidObject)
                    throw new InvalidOperationException($"第 {profileIndex + 1} 个轮廓实例已失效。");
                if (instance.Document != doc)
                    throw new InvalidOperationException($"第 {profileIndex + 1} 个轮廓不属于当前项目文档。");
                if (!profileIds.Add(instance.Id.ToString()))
                    throw new InvalidOperationException($"轮廓实例 {instance.Id} 被重复选择，不能用于 Loft。");

                GeometryElement? geometry = instance.get_Geometry(new Options
                {
                    ComputeReferences = false,
                    DetailLevel = ViewDetailLevel.Fine,
                    IncludeNonVisibleObjects = true
                });
                if (geometry == null)
                    throw new InvalidOperationException($"轮廓实例 {instance.Id} 没有可读取几何。");

                var clonedCurves = new List<Curve>();
                CloneCurvesFromGeometry(geometry, clonedCurves, endpointTolerance);
                clonedCurves = clonedCurves
                    .Where(curve => curve != null && curve.IsBound && curve.Length > endpointTolerance)
                    .ToList();

                if (clonedCurves.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"轮廓实例 {instance.Id} 中没有找到有效模型曲线。请确认它是由模型线组成的闭合轮廓族，而不是仅包含详图线、符号线或实体边。 ");
                }

                List<Curve> ordered;
                try
                {
                    ordered = BuildClosedCurveSequence(clonedCurves, endpointTolerance);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"第 {profileIndex + 1} 个轮廓（ID {instance.Id}）无法组成唯一闭合环。请检查断线、重叠线或多个独立环。\n{ex.Message}",
                        ex);
                }

                orderedProfiles.Add(ordered);
                foreach (Curve curve in ordered)
                {
                    // 仅用端点会遗漏斜放圆弧的 Z 极值；保守地读取整条曲线的离散点。
                    foreach (XYZ vertex in curve.Tessellate())
                        sourceZ.Include(vertex.Z);
                }

                DiagnosticLog.WriteInfo(
                    "Loft引擎",
                    $"轮廓 {profileIndex + 1}: InstanceId={instance.Id}; Curves={ordered.Count}");
            }

            if (orderedProfiles.Count < 2 || !sourceZ.IsValid)
                throw new InvalidOperationException("未获得至少两个有效闭合轮廓。");
            if (sourceZ.Height <= MinimumEndpointTolerance)
                throw new InvalidOperationException("轮廓没有有效的 Z 高度差，不能生成三维 Loft。");

            XYZ firstPoint = orderedProfiles[0][0].GetEndPoint(0);
            XYZ familyAnchor = new XYZ(firstPoint.X, firstPoint.Y, sourceZ.MinZ);
            Transform shiftTransform = Transform.CreateTranslation(new XYZ(-familyAnchor.X, -familyAnchor.Y, -familyAnchor.Z));

            var shiftedProfiles = new List<List<Curve>>();
            for (int profileIndex = 0; profileIndex < orderedProfiles.Count; profileIndex++)
            {
                List<Curve> shifted = orderedProfiles[profileIndex]
                    .Select(curve => curve.CreateTransformed(shiftTransform))
                    .ToList();

                if (profileIndex > 0 && shifted.Count == shiftedProfiles[0].Count)
                    shifted = AlignCurveSequence(shiftedProfiles[0], shifted);

                shiftedProfiles.Add(shifted);
            }

            IList<CurveLoop> profileLoops = shiftedProfiles
                .Select(curves => CurveLoop.Create(curves))
                .ToList();

            Solid loftSolid;
            try
            {
                loftSolid = GeometryCreationUtilities.CreateLoftGeometry(
                    profileLoops,
                    new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Revit 无法生成 Loft。轮廓必须闭合、无自交和退化边；圆形单曲线必须拆分为多段；轮廓顺序应按放样方向排列。\n" + ex.Message,
                    ex);
            }

            if (loftSolid == null || loftSolid.Volume <= 0)
                throw new InvalidOperationException("Revit 返回了无效的 Loft 实体（体积为 0）。");

            // 只有目标标高关联字段，不存在独立的 Base/Top Offset：实际上下界由源轮廓决定。
            // 校验局部实体上下界，避免标高/平移被重复解释导致实体整体变高或变矮。
            LoftZBounds localBounds = ReadSolidZBounds(loftSolid);
            CheckZBounds(localBounds, sourceZ.MinZ - familyAnchor.Z,
                sourceZ.MaxZ - familyAnchor.Z, "族局部 Loft 几何");
            DiagnosticLog.WriteInfo(
                "Loft引擎",
                $"Loft几何创建成功；Profiles={profileLoops.Count}; Volume={loftSolid.Volume:G8}; " +
                $"源世界Z=[{sourceZ.MinZ:G8},{sourceZ.MaxZ:G8}]; 局部实体Z=[{localBounds.MinZ:G8},{localBounds.MaxZ:G8}]");

            string? tempFile = null;
            try
            {
                tempFile = CreateFamilyFile(uiApp, state, loftSolid);
                Family loadedFamily = LoadAndPlaceFamily(doc, state, tempFile, familyAnchor, sourceZ);
                DiagnosticLog.WriteInfo("Loft引擎", $"族已载入并放置；FamilyId={loadedFamily.Id}");
            }
            finally
            {
                FamilyFileHelper.Cleanup(tempFile);
            }
        }

        private static string CreateFamilyFile(
            UIApplication uiApp,
            Models.EntityGeneratorState state,
            Solid loftSolid)
        {
            Document familyDocument = uiApp.Application.NewFamilyDocument(state.TemplatePath)
                ?? throw new InvalidOperationException("无法从所选 .rft 模板创建族文档。");

            string? tempFile = null;
            try
            {
                using (Transaction transaction = new Transaction(familyDocument, "创建放样实体"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动族文档事务。");

                    SetFamilyCategory(familyDocument, state.TargetCategory);

                    FreeFormElement freeForm = FreeFormElement.Create(familyDocument, loftSolid)
                        ?? throw new InvalidOperationException("FreeFormElement.Create 未返回有效实体。");

                    if (state.IsVoid)
                    {
                        Parameter? cuttingParameter = freeForm.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING);
                        if (cuttingParameter == null || cuttingParameter.IsReadOnly)
                            throw new InvalidOperationException("当前模板不支持把生成实体设置为 Void。");
                        cuttingParameter.Set(1);

                        if (state.CutWithVoids)
                        {
                            Parameter? allowCutParameter = familyDocument.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS);
                            if (allowCutParameter == null || allowCutParameter.IsReadOnly)
                                throw new InvalidOperationException("当前族模板不支持“载入时剪切空心”。");
                            allowCutParameter.Set(1);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(state.MaterialName))
                    {
                        FamilyParameter materialParameter = GetOrCreateMaterialParameter(familyDocument);
                        Parameter? formMaterialParameter = freeForm.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                        if (formMaterialParameter == null)
                            throw new InvalidOperationException("生成的 FreeFormElement 没有可关联的材质参数。");

                        familyDocument.FamilyManager.AssociateElementParameterToFamilyParameter(
                            formMaterialParameter,
                            materialParameter);
                    }
                    else
                    {
                        DiagnosticLog.WriteInfo("Loft引擎", "材质留空：不创建族材质参数，使用族类别/模板默认材质");
                    }

                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("族文档事务未能提交。");
                }

                // SaveAs 不能在本插件打开的任何项目 Transaction/TransactionGroup 内执行。
                tempFile = FamilyFileHelper.CreateTempFamilyPath(state.FamilyName, "YangTools_EntityGenerator");
                familyDocument.SaveAs(tempFile);
                DiagnosticLog.WriteInfo("Loft引擎", $"临时族已保存：{tempFile}");
                return tempFile;
            }
            catch
            {
                FamilyFileHelper.Cleanup(tempFile);
                throw;
            }
            finally
            {
                familyDocument.Close(false);
            }
        }

        private static Family LoadAndPlaceFamily(
            Document document,
            Models.EntityGeneratorState state,
            string familyFile,
            XYZ familyAnchor,
            LoftZBounds sourceZ)
        {
            Family? loadedFamily = null;

            using (TransactionGroup group = new TransactionGroup(document, "生成Loft实体"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动项目事务组。");

                try
                {
                    using (Transaction loadTransaction = new Transaction(document, "载入Loft族"))
                    {
                        if (loadTransaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动族载入事务。");

                        bool loaded = document.LoadFamily(familyFile, new FamilyOption(), out loadedFamily);
                        if (!loaded || loadedFamily == null)
                            throw new InvalidOperationException("生成的族文件未能载入当前项目。请检查同名族覆盖、模板或族类别设置。");

                        if (loadTransaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("族载入事务未能提交。");
                    }

                    using (Transaction placeTransaction = new Transaction(document, "放置Loft族实例"))
                    {
                        if (placeTransaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动族实例放置事务。");

                        FamilySymbol symbol = ResolveFirstSymbol(document, loadedFamily);
                        if (!symbol.IsActive)
                        {
                            symbol.Activate();
                            document.Regenerate();
                        }

                        Level targetLevel = ResolveTargetLevel(document, state.TargetLevelId, familyAnchor.Z);
                        FamilyInstance newInstance = FamilyPlacementHelper.CreateAtWorldPoint(
                            document,
                            symbol,
                            targetLevel,
                            familyAnchor,
                            "Loft族实例放置");

                        SetProjectMaterialIfRequested(document, newInstance, state.MaterialName);
                        document.Regenerate();
                        if (!state.IsVoid)
                        {
                            // 事务提交前直接读取项目中刚生成的实体，核对世界 Z 上下界。
                            // 仅比对 LocationPoint 会漏掉 FreeForm 本身被错误平移/缩放的问题。
                            LoftZBounds placedBounds = ReadPlacedInstanceZBounds(newInstance);
                            CheckZBounds(placedBounds, sourceZ.MinZ, sourceZ.MaxZ,
                                "已放置 Loft 实例世界几何");
                        }
                        else
                        {
                            DiagnosticLog.WriteInfo("Loft引擎", "Void 族可能没有可读取的正体积 Solid；已检查局部 Loft 高度，项目空心几何留待 Revit 实机验收。");
                        }

                        if (placeTransaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("族实例放置事务未能提交。");
                    }

                    if (group.Assimilate() != TransactionStatus.Committed)
                        throw new InvalidOperationException("项目事务组未能合并提交。");
                }
                catch (Exception originalError)
                {
                    if (group.GetStatus() == TransactionStatus.Started)
                    {
                        TransactionStatus rollback = group.RollBack();
                        if (rollback != TransactionStatus.RolledBack)
                            throw new InvalidOperationException(
                                "Loft 项目事务组回滚状态未知，请先检查生成族和实例是否残留，不要直接重复执行。",
                                originalError);
                    }
                    throw;
                }
            }

            return loadedFamily ?? throw new InvalidOperationException("生成族未成功载入项目。");
        }

        // 源轮廓、族局部实体、项目实例一律在同一 Z 坐标体系里比较上下界。
        // 曲线细分可能有近似误差：5 mm 容差仅用于检测明显的高度错误，不能替代实机几何验收。
        private const double LoftHeightToleranceFeet = 5.0 / 304.8;

        private sealed class LoftZBounds
        {
            public double MinZ { get; private set; } = double.PositiveInfinity;
            public double MaxZ { get; private set; } = double.NegativeInfinity;
            public bool IsValid => !double.IsInfinity(MinZ) && !double.IsInfinity(MaxZ);
            public double Height => MaxZ - MinZ;
            public void Include(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new InvalidOperationException("Loft 几何出现非法 Z 坐标。");
                MinZ = Math.Min(MinZ, value);
                MaxZ = Math.Max(MaxZ, value);
            }
        }

        private static LoftZBounds ReadSolidZBounds(Solid solid)
        {
            if (solid == null || solid.Volume <= 0)
                throw new InvalidOperationException("Loft 未能提供有效正体积 Solid。");
            BoundingBoxXYZ bbox = solid.GetBoundingBox()
                ?? throw new InvalidOperationException("Loft Solid 无法读取包围盒。");
            var bounds = new LoftZBounds();
            foreach (double x in new[] { bbox.Min.X, bbox.Max.X })
                foreach (double y in new[] { bbox.Min.Y, bbox.Max.Y })
                    foreach (double z in new[] { bbox.Min.Z, bbox.Max.Z })
                        bounds.Include(bbox.Transform.OfPoint(new XYZ(x, y, z)).Z);
            return bounds;
        }

        private static LoftZBounds ReadPlacedInstanceZBounds(FamilyInstance instance)
        {
            GeometryElement geometry = instance.get_Geometry(new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            }) ?? throw new InvalidOperationException("无法读取放置后的 Loft 实例几何。");
            var result = new LoftZBounds();
            AppendPlacedGeometryZ(geometry, result);
            if (!result.IsValid)
                throw new InvalidOperationException("放置后的 Loft 实例没有可校验的正体积实体，拒绝提交。");
            return result;
        }

        private static void AppendPlacedGeometryZ(GeometryElement geometry, LoftZBounds bounds)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Volume > 0)
                {
                    LoftZBounds part = ReadSolidZBounds(solid);
                    bounds.Include(part.MinZ);
                    bounds.Include(part.MaxZ);
                }
                else if (item is GeometryInstance nested)
                {
                    // GetInstanceGeometry 已返回项目世界几何，不能再次乘实例 Transform。
                    GeometryElement worldGeometry = nested.GetInstanceGeometry();
                    if (worldGeometry != null)
                        AppendPlacedGeometryZ(worldGeometry, bounds);
                }
            }
        }

        private static void CheckZBounds(LoftZBounds actual, double expectedMinZ,
            double expectedMaxZ, string context)
        {
            if (!actual.IsValid ||
                Math.Abs(actual.MinZ - expectedMinZ) > LoftHeightToleranceFeet ||
                Math.Abs(actual.MaxZ - expectedMaxZ) > LoftHeightToleranceFeet)
            {
                throw new InvalidOperationException(
                    $"{context}的高程范围不匹配。" +
                    $"期望=[{expectedMinZ:G8},{expectedMaxZ:G8}] ft，" +
                    $"实际=[{actual.MinZ:G8},{actual.MaxZ:G8}] ft；" +
                    "容差 5mm。为避免错误实体写入项目，本次操作必须回滚。");
            }
        }

        private static void ValidateState(Models.EntityGeneratorState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Profiles == null || state.Profiles.Count < 2)
                throw new InvalidOperationException("至少需要两个轮廓实例。");
            if (string.IsNullOrWhiteSpace(state.TemplatePath) || !File.Exists(state.TemplatePath))
                throw new FileNotFoundException("族模板文件不存在。", state.TemplatePath);
            if (!string.Equals(Path.GetExtension(state.TemplatePath), ".rft", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("族模板必须是 .rft 文件。");
            if (string.IsNullOrWhiteSpace(state.FamilyName))
                throw new InvalidOperationException("族名称不能为空。");
        }

        private static List<Curve> BuildClosedCurveSequence(IList<Curve> sourceCurves, double tolerance)
        {
            var remaining = sourceCurves
                .Where(curve => curve != null && curve.IsBound && curve.Length > tolerance)
                .ToList();
            if (remaining.Count < 2)
                throw new InvalidOperationException("有效曲线数量不足。Revit Loft 不允许单条闭合曲线作为轮廓。");

            var ordered = new List<Curve> { remaining[0] };
            remaining.RemoveAt(0);

            while (remaining.Count > 0)
            {
                XYZ currentEnd = ordered[ordered.Count - 1].GetEndPoint(1);
                int matchIndex = -1;
                bool reverse = false;
                double bestDistance = double.MaxValue;

                for (int i = 0; i < remaining.Count; i++)
                {
                    Curve candidate = remaining[i];
                    double startDistance = currentEnd.DistanceTo(candidate.GetEndPoint(0));
                    if (startDistance < bestDistance)
                    {
                        bestDistance = startDistance;
                        matchIndex = i;
                        reverse = false;
                    }

                    double endDistance = currentEnd.DistanceTo(candidate.GetEndPoint(1));
                    if (endDistance < bestDistance)
                    {
                        bestDistance = endDistance;
                        matchIndex = i;
                        reverse = true;
                    }
                }

                if (matchIndex < 0 || bestDistance > tolerance)
                {
                    throw new InvalidOperationException(
                        $"轮廓在第 {ordered.Count} 条边后断开；最近端点距离 {bestDistance:G8} 英尺，允许值 {tolerance:G8} 英尺。");
                }

                Curve next = remaining[matchIndex];
                remaining.RemoveAt(matchIndex);
                ordered.Add(reverse ? next.CreateReversed() : next);
            }

            double closureDistance = ordered[0].GetEndPoint(0)
                .DistanceTo(ordered[ordered.Count - 1].GetEndPoint(1));
            if (closureDistance > tolerance)
                throw new InvalidOperationException($"轮廓未闭合，首尾间距为 {closureDistance:G8} 英尺。");

            return ordered;
        }

        private static List<Curve> AlignCurveSequence(IList<Curve> reference, IList<Curve> candidate)
        {
            if (reference.Count != candidate.Count || reference.Count < 2)
                return candidate.ToList();

            List<Curve>? best = null;
            double bestScore = double.MaxValue;

            foreach (bool reverse in new[] { false, true })
            {
                List<Curve> oriented = reverse
                    ? candidate.Reverse().Select(curve => curve.CreateReversed()).ToList()
                    : candidate.ToList();

                for (int shift = 0; shift < oriented.Count; shift++)
                {
                    var shifted = new List<Curve>(oriented.Count);
                    for (int i = 0; i < oriented.Count; i++)
                        shifted.Add(oriented[(i + shift) % oriented.Count]);

                    double score = CalculateProfileAlignmentScore(reference, shifted);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = shifted;
                    }
                }
            }

            return best ?? candidate.ToList();
        }

        private static double CalculateProfileAlignmentScore(IList<Curve> reference, IList<Curve> candidate)
        {
            List<XYZ> referencePoints = reference.Select(curve => curve.GetEndPoint(0)).ToList();
            List<XYZ> candidatePoints = candidate.Select(curve => curve.GetEndPoint(0)).ToList();
            XYZ referenceCenter = AveragePoint(referencePoints);
            XYZ candidateCenter = AveragePoint(candidatePoints);

            double referenceScale = Math.Max(
                referencePoints.Average(point => point.DistanceTo(referenceCenter)),
                MinimumEndpointTolerance);
            double candidateScale = Math.Max(
                candidatePoints.Average(point => point.DistanceTo(candidateCenter)),
                MinimumEndpointTolerance);

            double score = 0;
            for (int i = 0; i < referencePoints.Count; i++)
            {
                XYZ refVector = referencePoints[i].Subtract(referenceCenter);
                XYZ candidateVector = candidatePoints[i].Subtract(candidateCenter);
                XYZ normalizedRef = new XYZ(refVector.X / referenceScale, refVector.Y / referenceScale, refVector.Z / referenceScale);
                XYZ normalizedCandidate = new XYZ(candidateVector.X / candidateScale, candidateVector.Y / candidateScale, candidateVector.Z / candidateScale);
                score += normalizedRef.DistanceTo(normalizedCandidate);
            }

            return score;
        }

        private static XYZ AveragePoint(IList<XYZ> points)
        {
            if (points.Count == 0) return XYZ.Zero;
            double x = 0;
            double y = 0;
            double z = 0;
            foreach (XYZ point in points)
            {
                x += point.X;
                y += point.Y;
                z += point.Z;
            }
            return new XYZ(x / points.Count, y / points.Count, z / points.Count);
        }

        private static void CloneCurvesFromGeometry(GeometryElement geometry, List<Curve> curves, double tolerance)
        {
            foreach (GeometryObject geometryObject in geometry)
            {
                if (geometryObject is Curve curve)
                {
                    AddDetachedCurve(curve, curves, tolerance);
                }
                else if (geometryObject is GeometryInstance geometryInstance)
                {
                    // GetInstanceGeometry 返回实例在项目坐标系中的几何副本，不再叠加 Transform。
                    GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                    if (instanceGeometry != null)
                        CloneCurvesFromGeometry(instanceGeometry, curves, tolerance);
                }
            }
        }

        private static void AddDetachedCurve(Curve curve, List<Curve> curves, double tolerance)
        {
            if (curve.IsBound && curve is Line line)
            {
                curves.Add(Line.CreateBound(line.GetEndPoint(0), line.GetEndPoint(1)));
                return;
            }

            if (curve.IsBound && curve is Arc arc)
            {
                double midParameter = (arc.GetEndParameter(0) + arc.GetEndParameter(1)) / 2.0;
                curves.Add(Arc.Create(arc.GetEndPoint(0), arc.GetEndPoint(1), arc.Evaluate(midParameter, false)));
                return;
            }

            IList<XYZ> points = curve.Tessellate();
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i].DistanceTo(points[i + 1]) > tolerance)
                    curves.Add(Line.CreateBound(points[i], points[i + 1]));
            }

            // 未绑定的周期曲线（常见为整圆）需补最后一段，避免被静默忽略。
            if (!curve.IsBound && points.Count > 2 && points[points.Count - 1].DistanceTo(points[0]) > tolerance)
                curves.Add(Line.CreateBound(points[points.Count - 1], points[0]));
        }

        private static void SetFamilyCategory(Document familyDocument, BuiltInCategory targetCategory)
        {
            Category? category = familyDocument.Settings.Categories.get_Item(targetCategory);
            if (category == null)
                throw new InvalidOperationException($"所选类别 {targetCategory} 不适用于当前族模板。");

            try
            {
                familyDocument.OwnerFamily.FamilyCategory = category;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法把新族设置为所选类别 {category.Name}。\n{ex.Message}", ex);
            }
        }

        private static FamilyParameter GetOrCreateMaterialParameter(Document familyDocument)
        {
            FamilyParameter? existing = familyDocument.FamilyManager.get_Parameter("Structural Material");
            if (existing != null) return existing;

#if REVIT2022_OR_GREATER
            return familyDocument.FamilyManager.AddParameter(
                "Structural Material",
                GroupTypeId.Materials,
                SpecTypeId.Reference.Material,
                true);
#else
            return familyDocument.FamilyManager.AddParameter(
                "Structural Material",
                BuiltInParameterGroup.PG_MATERIALS,
                ParameterType.Material,
                true);
#endif
        }

        private static FamilySymbol ResolveFirstSymbol(Document document, Family family)
        {
            ElementId? symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            if (symbolId == null || symbolId == ElementId.InvalidElementId)
                throw new InvalidOperationException("载入的族没有可用族类型。");

            return document.GetElement(symbolId) as FamilySymbol
                ?? throw new InvalidOperationException("无法读取载入族的族类型。");
        }

        private static Level ResolveTargetLevel(Document document, ElementId targetLevelId, double referenceElevation)
        {
            Level? level = targetLevelId != ElementId.InvalidElementId
                ? document.GetElement(targetLevelId) as Level
                : null;
            if (targetLevelId != ElementId.InvalidElementId && level == null)
                throw new InvalidOperationException("所选目标标高已失效，禁止悄悄改用其他标高。");

            level ??= new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(item => Math.Abs(item.ProjectElevation - referenceElevation))
                .FirstOrDefault();

            return level ?? throw new InvalidOperationException("项目中没有可用于放置族实例的标高。");
        }

        private static void SetProjectMaterialIfRequested(Document document, FamilyInstance instance, string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName)) return;

            Material? material = new FilteredElementCollector(document)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(item => item.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));
            if (material == null)
                throw new InvalidOperationException($"项目中已找不到材质“{materialName}”，无法完成材质赋值。");

            Parameter? materialParameter = instance.LookupParameter("Structural Material");
            if (materialParameter == null || materialParameter.IsReadOnly || materialParameter.StorageType != StorageType.ElementId)
                throw new InvalidOperationException("新实例没有可写的 Structural Material 参数，无法完成材质赋值。");

            materialParameter.Set(material.Id);
        }
    }
}
