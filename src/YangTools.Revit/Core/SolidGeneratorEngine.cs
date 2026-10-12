using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class SolidGeneratorEngine
    {
        public static void Execute(UIApplication uiApp, Document doc, Models.SolidGeneratorState state)
        {
            ValidateState(doc, state);

            Element sourceElement = doc.GetElement(state.SourceElementId)
                ?? throw new InvalidOperationException("无法获取选中的源图元。");

            DiagnosticLog.WriteInfo(
                "实体提取引擎",
                $"开始读取源图元；Id={sourceElement.Id}; Category={sourceElement.Category?.Name}; Type={sourceElement.GetType().FullName}");

            List<Solid> sourceSolids = ExtractSolids(sourceElement);
            if (sourceSolids.Count == 0)
                throw new InvalidOperationException("选中的图元没有可提取的实心几何体。请确认对象不是仅含网格、二维线、点云或空几何的图元。");

            SolidEnvelope sourceBounds = ReadSolidEnvelope(sourceSolids);
            XYZ center = CalculateCenter(sourceSolids);
            Transform toOrigin = Transform.CreateTranslation(new XYZ(-center.X, -center.Y, -center.Z));
            var localSolids = new List<Solid>();
            var transformErrors = new List<string>();

            for (int index = 0; index < sourceSolids.Count; index++)
            {
                try
                {
                    Solid transformed = SolidUtils.CreateTransformed(sourceSolids[index], toOrigin);
                    if (transformed == null || transformed.Volume <= 0 || transformed.Faces.Size == 0)
                        throw new InvalidOperationException("变换后的实体无效或体积为 0。");
                    localSolids.Add(transformed);
                }
                catch (Exception ex)
                {
                    transformErrors.Add($"实体 {index + 1}: {ex.Message}");
                }
            }

            if (transformErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "部分实体无法转换到族局部坐标。为避免生成缺失几何的错误族，本次操作已取消。\n" +
                    string.Join("\n", transformErrors));
            }

            VerifySolidEnvelope(
                ReadSolidEnvelope(localSolids), sourceBounds, new XYZ(-center.X, -center.Y, -center.Z),
                "局部实体几何");

            DiagnosticLog.WriteInfo(
                "实体提取引擎",
                $"几何读取成功；Solids={localSolids.Count}; Center=({center.X:G8},{center.Y:G8},{center.Z:G8})");

            string? tempFile = null;
            try
            {
                tempFile = CreateFamilyFile(uiApp, state, localSolids);
                Family loadedFamily = LoadAndPlaceFamily(doc, state, tempFile, center, sourceBounds);
                DiagnosticLog.WriteInfo("实体提取引擎", $"族已载入并放置；FamilyId={loadedFamily.Id}");
            }
            finally
            {
                FamilyFileHelper.Cleanup(tempFile);
            }
        }

        private static string CreateFamilyFile(
            UIApplication uiApp,
            Models.SolidGeneratorState state,
            IList<Solid> localSolids)
        {
            Document familyDocument = uiApp.Application.NewFamilyDocument(state.TemplatePath)
                ?? throw new InvalidOperationException("无法从所选 .rft 模板创建族文档。");

            string? tempFile = null;
            try
            {
                using (Transaction transaction = new Transaction(familyDocument, "生成实体"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动族文档事务。");

                    SetFamilyCategory(familyDocument, state.TargetCategory);
                    FamilyParameter? materialParameter = string.IsNullOrWhiteSpace(state.MaterialName)
                        ? null
                        : GetOrCreateMaterialParameter(familyDocument);

                    if (materialParameter == null)
                        DiagnosticLog.WriteInfo("实体提取引擎", "材质留空：不创建族材质参数，使用族类别/模板默认材质");

                    for (int index = 0; index < localSolids.Count; index++)
                    {
                        FreeFormElement form;
                        try
                        {
                            form = FreeFormElement.Create(familyDocument, localSolids[index])
                                ?? throw new InvalidOperationException("FreeFormElement.Create 未返回对象。");
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"第 {index + 1} 个实体无法写入族文档。\n{ex.Message}", ex);
                        }

                        if (materialParameter != null)
                        {
                            Parameter? formMaterialParameter = form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                            if (formMaterialParameter == null)
                                throw new InvalidOperationException($"第 {index + 1} 个 FreeFormElement 没有可关联的材质参数。");

                            familyDocument.FamilyManager.AssociateElementParameterToFamilyParameter(
                                formMaterialParameter,
                                materialParameter);
                        }
                    }

                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("族文档事务未能提交。");
                }

                // Family Document 的 SaveAs 必须在插件打开的项目事务/事务组之外执行。
                tempFile = FamilyFileHelper.CreateTempFamilyPath(state.FamilyName, "YangTools_SolidGenerator");
                familyDocument.SaveAs(tempFile);
                DiagnosticLog.WriteInfo("实体提取引擎", $"临时族已保存：{tempFile}");
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
            Models.SolidGeneratorState state,
            string familyFile,
            XYZ center,
            SolidEnvelope sourceBounds)
        {
            Family? loadedFamily = null;
            ElementId placedInstanceId = ElementId.InvalidElementId;

            using (TransactionGroup group = new TransactionGroup(document, "生成实体族"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动项目事务组。");

                try
                {
                    using (Transaction loadTransaction = new Transaction(document, "载入实体族"))
                    {
                        if (loadTransaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动族载入事务。");

                        bool loaded = document.LoadFamily(familyFile, new FamilyOption(), out loadedFamily);
                        if (!loaded || loadedFamily == null)
                            throw new InvalidOperationException("生成的族文件未能载入当前项目。请检查同名族覆盖、模板或族类别设置。");

                        if (loadTransaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("族载入事务未能提交。");
                    }

                    using (Transaction placeTransaction = new Transaction(document, "放置实体族实例"))
                    {
                        if (placeTransaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动族实例放置事务。");

                        FamilySymbol symbol = ResolveFirstSymbol(document, loadedFamily);
                        if (!symbol.IsActive)
                        {
                            symbol.Activate();
                            document.Regenerate();
                        }

                        Level targetLevel = ResolveTargetLevel(document, state.TargetLevelId, center.Z);
                        FamilyInstance newInstance = FamilyPlacementHelper.CreateAtWorldPoint(
                            document,
                            symbol,
                            targetLevel,
                            center,
                            "实体提取族实例放置");

                        placedInstanceId = newInstance.Id;
                        SetProjectMaterialIfRequested(document, newInstance, state.MaterialName);
                        VerifyPlacedGeometry(newInstance, sourceBounds);

                        if (placeTransaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("族实例放置事务未能提交。");
                    }

                    // 在事务组合并前二次确认；发现变形还能撤销载入与放置两个事务。
                    FamilyInstance? committedInstance = document.GetElement(placedInstanceId) as FamilyInstance;
                    if (committedInstance == null)
                        throw new InvalidOperationException("族实例提交后无法回读，本次项目事务组必须回滚。");
                    VerifyPlacedGeometry(committedInstance, sourceBounds);

                    if (group.Assimilate() != TransactionStatus.Committed)
                        throw new InvalidOperationException("项目事务组未能合并提交。");
                }
                catch (Exception operationError)
                {
                    TransactionStatus groupStatus = group.GetStatus();
                    if (groupStatus == TransactionStatus.Started)
                    {
                        try
                        {
                            TransactionStatus rollback = group.RollBack();
                            if (rollback != TransactionStatus.RolledBack)
                                throw new InvalidOperationException($"事务组回滚结果为 {rollback}。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                "实体提取失败，且项目事务组回滚状态无法确认。请先检查项目模型，勿直接重试。",
                                new AggregateException(operationError, rollbackError));
                        }
                    }
                    else if (groupStatus != TransactionStatus.RolledBack)
                    {
                        throw new InvalidOperationException(
                            $"实体提取异常，项目事务组状态为 {groupStatus}，无法证明全部回滚。请检查模型后再操作。",
                            operationError);
                    }
                    throw;
                }
            }

            return loadedFamily ?? throw new InvalidOperationException("生成族未成功载入项目。");
        }

        private static void ValidateState(Document document, Models.SolidGeneratorState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.SourceElementId == ElementId.InvalidElementId || document.GetElement(state.SourceElementId) == null)
                throw new InvalidOperationException("源图元无效或已被删除。");
            if (string.IsNullOrWhiteSpace(state.TemplatePath) || !File.Exists(state.TemplatePath))
                throw new FileNotFoundException("族模板文件不存在。", state.TemplatePath);
            if (!string.Equals(Path.GetExtension(state.TemplatePath), ".rft", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("族模板必须是 .rft 文件。");
            if (string.IsNullOrWhiteSpace(state.FamilyName))
                throw new InvalidOperationException("族名称不能为空。");
            if (state.TargetLevelId != ElementId.InvalidElementId &&
                !(document.GetElement(state.TargetLevelId) is Level))
                throw new InvalidOperationException("所选目标标高已失效。请在窗口中重新选择标高，不允许静默切换到其他楼层。");
        }

        public static List<Solid> ExtractSolids(Element element)
        {
            var solids = new List<Solid>();
            var options = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = true
            };

            GeometryElement? geometry = element.get_Geometry(options);
            if (geometry != null)
                CollectSolids(geometry, solids);
            return solids;
        }

        private static void CollectSolids(GeometryElement geometry, List<Solid> solids)
        {
            foreach (GeometryObject geometryObject in geometry)
            {
                if (geometryObject is Solid solid && solid.Volume > 0 && solid.Faces.Size > 0)
                {
                    solids.Add(solid);
                }
                else if (geometryObject is GeometryInstance geometryInstance)
                {
                    // GetInstanceGeometry 已经位于项目坐标系，不能再次应用 GeometryInstance.Transform。
                    GeometryElement instanceGeometry = geometryInstance.GetInstanceGeometry();
                    if (instanceGeometry != null)
                        CollectSolids(instanceGeometry, solids);
                }
            }
        }

        // 几何 envelope 使用 Solid 本地包围盒的 Transform；不假设 bbox 坐标已在项目坐标系。
        private sealed class SolidEnvelope
        {
            public double MinX = double.PositiveInfinity, MinY = double.PositiveInfinity, MinZ = double.PositiveInfinity;
            public double MaxX = double.NegativeInfinity, MaxY = double.NegativeInfinity, MaxZ = double.NegativeInfinity;
            public double TotalVolume;
            public int SolidCount;

            public void Include(XYZ point)
            {
                MinX = Math.Min(MinX, point.X); MaxX = Math.Max(MaxX, point.X);
                MinY = Math.Min(MinY, point.Y); MaxY = Math.Max(MaxY, point.Y);
                MinZ = Math.Min(MinZ, point.Z); MaxZ = Math.Max(MaxZ, point.Z);
            }
        }

        private const double GeometryToleranceFeet = 5.0 / 304.8; // 5 mm（边界）

        private static SolidEnvelope ReadSolidEnvelope(IList<Solid> solids)
        {
            var result = new SolidEnvelope();
            foreach (Solid solid in solids)
            {
                if (solid == null || solid.Volume <= 0 || solid.Faces.Size == 0)
                    throw new InvalidOperationException("存在无效 Solid，禁止只提取部分几何。");
                BoundingBoxXYZ bbox = solid.GetBoundingBox()
                    ?? throw new InvalidOperationException("Solid 无法读取包围盒，无法证明几何完整。");
                Transform transform = bbox.Transform ?? Transform.Identity;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    XYZ corner = new XYZ(x == 0 ? bbox.Min.X : bbox.Max.X,
                                         y == 0 ? bbox.Min.Y : bbox.Max.Y,
                                         z == 0 ? bbox.Min.Z : bbox.Max.Z);
                    XYZ worldPoint = transform.OfPoint(corner);
                    if (double.IsNaN(worldPoint.X) || double.IsNaN(worldPoint.Y) || double.IsNaN(worldPoint.Z) ||
                        double.IsInfinity(worldPoint.X) || double.IsInfinity(worldPoint.Y) || double.IsInfinity(worldPoint.Z))
                        throw new InvalidOperationException("Solid 包围盒坐标非法。");
                    result.Include(worldPoint);
                }
                result.TotalVolume += solid.Volume;
                result.SolidCount++;
            }
            if (result.SolidCount == 0)
                throw new InvalidOperationException("没有可以校验的实体几何。");
            return result;
        }

        private static void VerifySolidEnvelope(SolidEnvelope actual, SolidEnvelope expected, XYZ translation, string stage)
        {
            // 实例生成后实体可被 Revit 合并，所以不要求 SolidCount 相同，
            // 但必须严格检查全部轴向边界和总体积，不能仅比较插入点。
            double[] observed = { actual.MinX, actual.MinY, actual.MinZ, actual.MaxX, actual.MaxY, actual.MaxZ };
            double[] reference = {
                expected.MinX + translation.X, expected.MinY + translation.Y, expected.MinZ + translation.Z,
                expected.MaxX + translation.X, expected.MaxY + translation.Y, expected.MaxZ + translation.Z
            };
            for (int index = 0; index < observed.Length; index++)
            {
                if (Math.Abs(observed[index] - reference[index]) > GeometryToleranceFeet)
                    throw new InvalidOperationException(
                        $"{stage}的世界几何外包范围与原图元不一致（轴 {index}；误差 {Math.Abs(observed[index] - reference[index]) * 304.8:G6} mm）。本次操作已拒绝。");
            }
            double volumeTolerance = Math.Max(1e-6, expected.TotalVolume * 0.01); // 1% 上限
            if (Math.Abs(actual.TotalVolume - expected.TotalVolume) > volumeTolerance)
                throw new InvalidOperationException(
                    $"{stage}总体积与源图元不一致（原 {expected.TotalVolume:G8} ft³，当前 {actual.TotalVolume:G8} ft³）。为避免缺失 Solid，本次操作已拒绝。");
        }

        private static void VerifyPlacedGeometry(FamilyInstance instance, SolidEnvelope sourceBounds)
        {
            List<Solid> placed = ExtractSolids(instance);
            SolidEnvelope actual = ReadSolidEnvelope(placed);
            VerifySolidEnvelope(actual, sourceBounds, XYZ.Zero, "放置族实例");
        }

        private static XYZ CalculateCenter(IList<Solid> solids)
        {
            if (solids.Count == 0) return XYZ.Zero;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;
            bool foundPoint = false;

            foreach (Solid solid in solids)
            {
                BoundingBoxXYZ? boundingBox = solid.GetBoundingBox();
                if (boundingBox == null) continue;

                Transform transform = boundingBox.Transform ?? Transform.Identity;
                for (int xIndex = 0; xIndex <= 1; xIndex++)
                for (int yIndex = 0; yIndex <= 1; yIndex++)
                for (int zIndex = 0; zIndex <= 1; zIndex++)
                {
                    XYZ localPoint = new XYZ(
                        xIndex == 0 ? boundingBox.Min.X : boundingBox.Max.X,
                        yIndex == 0 ? boundingBox.Min.Y : boundingBox.Max.Y,
                        zIndex == 0 ? boundingBox.Min.Z : boundingBox.Max.Z);
                    XYZ point = transform.OfPoint(localPoint);

                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    minZ = Math.Min(minZ, point.Z);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                    maxZ = Math.Max(maxZ, point.Z);
                    foundPoint = true;
                }
            }

            if (!foundPoint)
                return solids[0].ComputeCentroid();

            return new XYZ(
                (minX + maxX) / 2.0,
                (minY + maxY) / 2.0,
                (minZ + maxZ) / 2.0);
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
                throw new InvalidOperationException("指定目标标高已被删除，禁止自动换用其他楼层。");

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

            if (!materialParameter.Set(material.Id))
                throw new InvalidOperationException("材质参数写入返回 false，不能宣称赋值成功。");
            document.Regenerate();
            if (materialParameter.AsElementId() != material.Id)
                throw new InvalidOperationException("材质参数回读与请求不一致，本次操作必须回滚。");
        }
    }
}
