using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public class FaceBasedConverterEngine
    {
        private readonly UIApplication _uiapp;
        private readonly List<string> _createdTempPaths = new List<string>();

        public FaceBasedConverterEngine(UIApplication uiapp)
        {
            _uiapp = uiapp;
        }

        public string ResolveTemplatePath()
        {
            try
            {
                string templateDir = _uiapp.Application.FamilyTemplatePath;
                if (string.IsNullOrEmpty(templateDir))
                {
                    string version = _uiapp.Application.VersionNumber;
                    templateDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Autodesk", $"RVT {version}", "Family Templates", "Chinese");
                }

                string templatePath = Path.Combine(templateDir, "公制常规模型.rft");
                if (!File.Exists(templatePath))
                    templatePath = Path.Combine(templateDir, "Generic Model.rft");
                if (!File.Exists(templatePath))
                {
                    string engDir = templateDir.Replace("Chinese", "English");
                    templatePath = Path.Combine(engDir, "Metric Generic Model.rft");
                }
                if (File.Exists(templatePath))
                    return templatePath;
            }
            catch { }
            return null;
        }

        public bool IsFamilyFaceBased(Family fam)
        {
            if (fam == null) return false;
            try
            {
                // WorkPlaneBased 明确表示族放置在工作平面或几何面上。
                // 不能用 Host != null 判断，否则门窗等墙宿主族也会被误纳入。
                return fam.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased;
            }
            catch { return false; }
        }

        public Tuple<List<Solid>, List<Curve>> ExtractGeometry(FamilyInstance instance, bool bakeRotation)
        {
            var solids = new List<Solid>();
            var curves = new List<Curve>();
            try
            {
                var options = new Options
                {
                    ComputeReferences = false,
                    DetailLevel = ViewDetailLevel.Fine
                };
                var geomElem = instance.get_Geometry(options);
                if (geomElem == null) return Tuple.Create(solids, curves);

                Transform tApply;
                if (bakeRotation)
                {
                    XYZ locationPoint = XYZ.Zero;
                    if (instance.Location is LocationPoint lp)
                        locationPoint = lp.Point;
                    else
                    {
                        var bbox = instance.get_BoundingBox(null);
                        if (bbox != null) locationPoint = (bbox.Min + bbox.Max) / 2.0;
                    }
                    tApply = Transform.CreateTranslation(-locationPoint);
                }
                else
                {
                    tApply = instance.GetTransform().Inverse;
                }
                CollectGeometry(geomElem, tApply, solids, curves);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("提取或变换基于面族几何失败，不能生成可能缺失几何的转换族。", ex);
            }
            return Tuple.Create(solids, curves);
        }

        private void CollectGeometry(GeometryElement geomElem, Transform transform, List<Solid> solids, List<Curve> curves)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    try
                    {
                        Solid transformed = SolidUtils.CreateTransformed(solid, transform);
                        if (transformed != null && transformed.Volume > 0)
                            solids.Add(transformed);
                        else
                            throw new InvalidOperationException("Solid 变换后无有效体积。");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Solid 坐标变换失败，不能退回原坐标。", ex);
                    }
                }
                else if (geomObj is Curve curve)
                {
                    try
                    {
                        Curve transformed = curve.CreateTransformed(transform);
                        if (transformed != null) curves.Add(transformed);
                        else throw new InvalidOperationException("Curve 变换失败。");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Curve 坐标变换失败，不能退回原坐标。", ex);
                    }
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    var instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null) CollectGeometry(instGeom, transform, solids, curves);
                }
            }
        }

        public string CreateConvertedFamily(string familyName, string typeName, string suffix, List<Solid> solids, List<Curve> curves, string templatePath)
        {
            // 每个原始类型生成独立族，避免同一家族的多个类型互相覆盖。
            string newFamilyName = familyName + "_" + typeName + "_Converted" + suffix;
            string tempFile = FamilyFileHelper.CreateTempFamilyPath(newFamilyName, "YangTools_FBConvert");
            _createdTempPaths.Add(tempFile);

            Document famDoc = _uiapp.Application.NewFamilyDocument(templatePath);
            if (famDoc == null) throw new Exception($"无法使用模板 [{templatePath}] 创建族文档。");

            try
            {
                using (Transaction t = new Transaction(famDoc, "创建转换族"))
                {
                    if (t.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动转换族文档事务。");

                    foreach (var solid in solids)
                    {
                        try { FreeFormElement.Create(famDoc, solid); }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("转换族 FreeForm 几何创建失败，不允许静默缺失。", ex);
                        }
                    }

                    foreach (var curve in curves)
                    {
                        try
                        {
                            Plane plane = null;
                            if (curve is Line line)
                            {
                                XYZ pt1 = line.GetEndPoint(0);
                                XYZ dir = line.Direction;
                                XYZ up = XYZ.BasisZ;
                                if (dir.IsAlmostEqualTo(up) || dir.IsAlmostEqualTo(-up)) up = XYZ.BasisX;
                                XYZ normal = dir.CrossProduct(up).Normalize();
                                plane = Plane.CreateByNormalAndOrigin(normal, pt1);
                            }
                            else if (curve is Arc arc)
                            {
                                plane = Plane.CreateByNormalAndOrigin(arc.Normal, arc.Center);
                            }
                            else
                            {
                                XYZ pt1 = curve.GetEndPoint(0);
                                XYZ pt2 = curve.GetEndPoint(1);
                                XYZ dir = (pt2 - pt1).Normalize();
                                XYZ up = XYZ.BasisZ;
                                if (dir.IsAlmostEqualTo(up) || dir.IsAlmostEqualTo(-up)) up = XYZ.BasisX;
                                XYZ normal = dir.CrossProduct(up).Normalize();
                                plane = Plane.CreateByNormalAndOrigin(normal, pt1);
                            }

                            if (plane != null)
                            {
                                SketchPlane sp = SketchPlane.Create(famDoc, plane);
                                famDoc.FamilyCreate.NewModelCurve(curve, sp);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("转换族曲线创建失败，不允许静默丢失。", ex);
                        }
                    }

                    try
                    {
                        var category = famDoc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);
                        if (category != null) famDoc.OwnerFamily.FamilyCategory = category;

                        var alwaysVertical = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
                        if (alwaysVertical != null && !alwaysVertical.IsReadOnly) alwaysVertical.Set(0);

                        var workPlaneBased = famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
                        if (workPlaneBased != null && !workPlaneBased.IsReadOnly) workPlaneBased.Set(0);
                    }
                    catch { }

                    if (t.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("转换族文档事务提交未成功。");
                }

                famDoc.SaveAs(tempFile);
            }
            finally
            {
                famDoc.Close(false);
            }

            return tempFile;
        }

        public string GetOrientationHash(FamilyInstance inst)
        {
            try
            {
                var t = inst.GetTransform();
                return $"{Math.Round(t.BasisX.X, 3)}_{Math.Round(t.BasisX.Y, 3)}_{Math.Round(t.BasisX.Z, 3)}_" +
                       $"{Math.Round(t.BasisZ.X, 3)}_{Math.Round(t.BasisZ.Y, 3)}_{Math.Round(t.BasisZ.Z, 3)}";
            }
            catch { return "default"; }
        }

        /// <summary>
        /// strict=true 对应「删除原实例」：只要存在不能确认保留的用户参数，必须抛出异常并回滚。
        /// strict=false 保持此前非破坏性转换的尽力复制行为。
        /// </summary>
        public void CopyInstanceParameters(FamilyInstance source, FamilyInstance target, bool strict = false)
        {
            if (strict)
            {
                var repeatedNames = source.GetOrderedParameters()
                    .Where(p => p != null && p.Id.GetIdValue() >= 0 && p.HasValue && p.Definition != null)
                    .GroupBy(p => p.Definition.Name)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (repeatedNames.Length > 0)
                    throw new InvalidOperationException("原实例含同名用户参数，无法按名称无损复制：" + string.Join("、", repeatedNames));
            }
            foreach (Parameter srcParam in source.GetOrderedParameters())
            {
                if (srcParam == null || srcParam.Id.GetIdValue() < 0) continue;
                string name = srcParam.Definition?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (srcParam.StorageType == StorageType.None || !srcParam.HasValue) continue;

                try
                {
                    var matches = target.GetParameters(name);
                    if (strict && matches.Count != 1)
                        throw new InvalidOperationException($"参数 {name} 找不到唯一目标参数，删除将丢失数据。");
                    Parameter tgtParam = matches.FirstOrDefault();
                    if (strict && tgtParam != null &&
                        (tgtParam.IsShared != srcParam.IsShared ||
                         (srcParam.IsShared && tgtParam.GUID != srcParam.GUID)))
                        throw new InvalidOperationException($"参数 {name} 的共享参数 GUID 不一致，拒绝删除原实例。");
                    if (tgtParam == null || tgtParam.IsReadOnly || tgtParam.StorageType != srcParam.StorageType)
                    {
                        if (strict) throw new InvalidOperationException($"参数 {name} 不可安全复制，不能删除原实例。");
                        continue;
                    }

                    switch (srcParam.StorageType)
                    {
                        case StorageType.String:
                            tgtParam.Set(srcParam.AsString() ?? "");
                            if (strict && tgtParam.AsString() != srcParam.AsString())
                                throw new InvalidOperationException($"参数 {name} 回读不一致。");
                            break;
                        case StorageType.Double:
                            tgtParam.Set(srcParam.AsDouble());
                            if (strict && Math.Abs(tgtParam.AsDouble() - srcParam.AsDouble()) > 1e-8)
                                throw new InvalidOperationException($"参数 {name} 回读不一致。");
                            break;
                        case StorageType.Integer:
                            tgtParam.Set(srcParam.AsInteger());
                            if (strict && tgtParam.AsInteger() != srcParam.AsInteger())
                                throw new InvalidOperationException($"参数 {name} 回读不一致。");
                            break;
                        case StorageType.ElementId:
                            tgtParam.Set(srcParam.AsElementId());
                            if (strict && tgtParam.AsElementId().GetIdValue() != srcParam.AsElementId().GetIdValue())
                                throw new InvalidOperationException($"参数 {name} 回读不一致。");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (strict)
                        throw new InvalidOperationException($"无法完整保留原实例参数 {name}；已阻止删除原实例。", ex);
                    DiagnosticLog.WriteError("基于面转换参数尽力复制", ex);
                }
            }
        }

        public void CleanupTempFiles()
        {
            // 每次构建使用独立 GUID 子目录；只清理本窗口创建的文件。
            // 不得删除整个 YangTools_FBConvert 根目录，否则会误删其他 Revit 实例的临时族。
            foreach (string path in _createdTempPaths)
                FamilyFileHelper.Cleanup(path);
            _createdTempPaths.Clear();
        }
    }
}
