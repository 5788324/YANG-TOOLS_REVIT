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
    /// R00 钢筋能力探针：只读读取现有 Rebar 的宿主、Shape/Free Form 模式、
    /// 排布、约束、Hook 与中心线几何。该服务不得启动 Transaction，也不得修改模型。
    /// UI 与 MCP 共享本服务，作为后续 Shape Driven / Free Form 双引擎开发的事实基线。
    /// </summary>
    public static class RebarInspectionService
    {
        private const int MaxItems = 500;
        private const int MaxConstraintsPerBar = 48;
        private const int MaxCurvesPerSample = 64;

        public static RebarInspectionReport Inspect(UIApplication uiapp, IEnumerable<ElementId> elementIds)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) throw new InvalidOperationException("当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            List<ElementId> ids = (elementIds ?? Enumerable.Empty<ElementId>())
                .Where(id => id != null && id != ElementId.InvalidElementId)
                .Distinct(new ElementIdComparer())
                .Take(MaxItems)
                .ToList();

            var report = new RebarInspectionReport
            {
                RevitVersion = uiapp.Application.VersionNumber ?? string.Empty,
                DocumentTitle = doc.Title ?? string.Empty,
                InspectedAt = DateTime.Now,
                RequestedCount = ids.Count
            };

            foreach (ElementId id in ids)
            {
                Element? element = doc.GetElement(id);
                if (element is not Autodesk.Revit.DB.Structure.Rebar rebar)
                {
                    if (element != null)
                        report.Warnings.Add($"ElementId {id.GetIdValue()} 不是 Rebar，已跳过：{element.GetType().Name}");
                    continue;
                }

                report.Items.Add(InspectOne(doc, rebar));
            }

            report.RebarCount = report.Items.Count;
            if (report.RequestedCount >= MaxItems)
                report.Warnings.Add($"R00 单次最多检查 {MaxItems} 个 ElementId；更多钢筋请分批检查。");
            return report;
        }

        public static IReadOnlyList<ElementId> GetSelectedRebarIds(UIDocument uidoc)
        {
            if (uidoc == null) return Array.Empty<ElementId>();
            return uidoc.Selection.GetElementIds()
                .Where(id => uidoc.Document.GetElement(id) is Autodesk.Revit.DB.Structure.Rebar)
                .Take(MaxItems)
                .ToList();
        }

        private static RebarInspectionItem InspectOne(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar)
        {
            var item = new RebarInspectionItem
            {
                ElementId = rebar.Id.GetIdValue(),
                UniqueId = rebar.UniqueId ?? string.Empty
            };

            try
            {
                bool freeForm = rebar.IsRebarFreeForm();
                item.EngineMode = freeForm ? "FreeForm" : "ShapeDriven";
                item.RebarStyle = ReadRebarStyle(doc, rebar, freeForm);
                item.ScheduleMark = ReadOptionalMember(rebar, "ScheduleMark")?.ToString() ?? string.Empty;

                Element? barType = doc.GetElement(rebar.GetTypeId());
                item.BarTypeName = barType?.Name ?? string.Empty;
                item.BarDiameterMm = ReadDiameterMillimeters(barType);

                InspectHost(doc, rebar, item);
                InspectShape(doc, rebar, item);
                InspectLayout(rebar, item);
                InspectHooks(doc, rebar, item);
                InspectEndRelationships(rebar, item);
                InspectConstraints(doc, rebar, item);
                InspectSampleBarPositions(doc, rebar, item);
                InspectCurves(rebar, item);

                if (freeForm)
                    InspectFreeForm(rebar, item);
                else
                    InspectShapeDriven(rebar, item);
            }
            catch (Exception ex)
            {
                item.Error = ex.GetType().Name + ": " + ex.Message;
            }

            return item;
        }

        private static string ReadRebarStyle(
            Document doc,
            Autodesk.Revit.DB.Structure.Rebar rebar,
            bool freeForm)
        {
            try
            {
                if (freeForm)
                {
#if REVIT2021
                    // Revit 2021 API has no RebarFreeFormAccessor.RebarStyle.
                    // Unknown is not Standard; do not misreport the rebar style.
                    return "UNAVAILABLE_REVIT2021_FREEFORM_STYLE";
#else
                    using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                        return accessor.RebarStyle.ToString();
#endif
                }

                ElementId shapeId = rebar.GetShapeId();
                if (doc.GetElement(shapeId) is RebarShape shape)
                    return shape.RebarStyle.ToString();
            }
            catch { }

            return ReadOptionalMember(rebar, "Style")?.ToString() ?? string.Empty;
        }

        private static void InspectHost(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                ElementId hostId = rebar.GetHostId();
                item.HostId = hostId.GetIdValue();
                Element? host = doc.GetElement(hostId);
                item.HostUniqueId = host?.UniqueId ?? string.Empty;
                item.HostName = host?.Name ?? string.Empty;
                item.HostCategory = host?.Category?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                item.Notes.Add("读取宿主失败：" + ex.Message);
            }
        }

        private static void InspectShape(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                ElementId shapeId = rebar.GetShapeId();
                item.ShapeId = shapeId.GetIdValue();
                item.ShapeName = doc.GetElement(shapeId)?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                item.Notes.Add("Shape 无法按单一 ShapeId 读取：" + ex.Message);
            }
        }

        private static void InspectLayout(Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try { item.LayoutRule = rebar.LayoutRule.ToString(); }
            catch (Exception ex) { item.Notes.Add("读取 LayoutRule 失败：" + ex.Message); }

            try
            {
                item.DistributionType = ReadOptionalMember(rebar, "DistributionType")?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item.DistributionType))
                {
                    using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
                    {
                        object? varying = ReadOptionalMember(accessor, "UseRebarConstraintsToProduceVaryingBars");
                        if (varying is bool varyingBool)
                            item.DistributionType = varyingBool ? "VaryingLength" : "Uniform";
                    }
                }
            }
            catch (Exception ex) { item.Notes.Add("读取 DistributionType 失败：" + ex.Message); }

            try { item.NumberOfBarPositions = rebar.NumberOfBarPositions; }
            catch (Exception ex) { item.Notes.Add("读取 NumberOfBarPositions 失败：" + ex.Message); }

            try
            {
                double spacing = rebar.MaxSpacing;
                if (spacing > 0) item.MaxSpacingMm = ToMillimeters(spacing);
            }
            catch { }

            item.CanHaveVaryingLengthBars = ReadOptionalBoolMember(rebar, "CanHaveVaryingLengthBars");
            item.HasVariableLengthBars = ReadOptionalBoolMember(rebar, "HasVariableLengthBars");
            item.ConstraintsCanBeEdited = ReadOptionalBoolMember(rebar, "ConstraintsCanBeEdited");

            // HasVariableLengthBars 的官方说明要求在最新 regeneration 后读取才最准确。
            if (item.HasVariableLengthBars.HasValue)
                item.Notes.Add("HasVariableLengthBars 依赖当前文档已完成 regeneration；R00 为只读探针，不主动 Regenerate。 ");
        }

        private static void InspectShapeDriven(Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
                {
                    try { item.ArrayLengthMm = ToMillimeters(accessor.ArrayLength); } catch { }
                    try { item.BarsOnNormalSide = accessor.BarsOnNormalSide; } catch { }
                    item.UseConstraintsForVaryingBars = ReadOptionalBoolMember(accessor, "UseRebarConstraintsToProduceVaryingBars");
                    item.MultiplanarDepthMm = ReadOptionalDoubleMemberMillimeters(accessor, "MultiplanarDepth");
                }
            }
            catch (Exception ex)
            {
                item.Notes.Add("ShapeDrivenAccessor 读取失败：" + ex.Message);
            }
        }

        private static void InspectFreeForm(Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                {
                    item.FreeFormHasValidServer = ReadOptionalBoolMember(accessor, "HasValidServer");
                    item.FreeFormHasValidAlignedServer = ReadOptionalBoolMember(accessor, "HasValidAlignedServer");
                    item.FreeFormIsUnconstrained = ReadOptionalBoolMember(accessor, "IsUnconstrained");
                    object? guid = InvokeOptional(accessor, "GetServerGUID");
                    if (guid != null) item.FreeFormServerGuid = guid.ToString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                item.Notes.Add("FreeFormAccessor 读取失败：" + ex.Message);
            }
        }

        private static void InspectHooks(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            item.StartHookType = GetHookTypeName(doc, rebar, 0);
            item.EndHookType = GetHookTypeName(doc, rebar, 1);
            item.StartHookTypeId = GetHookTypeIdValue(rebar, 0);
            item.EndHookTypeId = GetHookTypeIdValue(rebar, 1);
        }

        private static long? GetHookTypeIdValue(Autodesk.Revit.DB.Structure.Rebar rebar, int end)
        {
            try
            {
                ElementId id = rebar.GetHookTypeId(end);
                if (id == ElementId.InvalidElementId) return null;
                return id.GetIdValue();
            }
            catch { return null; }
        }

        private static string GetHookTypeName(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, int end)
        {
            try
            {
                ElementId id = rebar.GetHookTypeId(end);
                if (id == ElementId.InvalidElementId) return string.Empty;
                return doc.GetElement(id)?.Name ?? id.GetIdValue().ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void InspectEndRelationships(Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            item.StartCouplerId = ReadOptionalElementIdValue(rebar, "GetCouplerId", 0);
            item.EndCouplerId = ReadOptionalElementIdValue(rebar, "GetCouplerId", 1);

            object? startLap = InvokeOptional(rebar, "GetLapLength", 0);
            object? endLap = InvokeOptional(rebar, "GetLapLength", 1);
            if (startLap is double startLapValue && startLapValue > 0) item.StartLapLengthMm = ToMillimeters(startLapValue);
            if (endLap is double endLapValue && endLapValue > 0) item.EndLapLengthMm = ToMillimeters(endLapValue);
        }

        private static void InspectConstraints(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                using (RebarConstraintsManager manager = rebar.GetRebarConstraintsManager())
                {
                    IList<RebarConstrainedHandle>? handles = null;
                    IList<RebarConstrainedHandle>? constrainedHandles = null;
                    try { handles = manager.GetAllHandles(); item.ConstraintHandleCount = handles.Count; } catch { }
                    try { constrainedHandles = manager.GetAllConstrainedHandles(); item.ConstrainedHandleCount = constrainedHandles.Count; } catch { }

                    if (constrainedHandles == null) return;
                    foreach (RebarConstrainedHandle handle in constrainedHandles.Take(MaxConstraintsPerBar))
                    {
                        try
                        {
                            var detail = new RebarConstraintInspection
                            {
                                HandleName = InvokeOptional(handle, "GetHandleName")?.ToString() ?? string.Empty,
                                HandleType = InvokeOptional(handle, "GetHandleType")?.ToString() ?? string.Empty,
                                HandleBehavior = ReadOptionalMember(handle, "HandleBehavior")?.ToString() ?? string.Empty
                            };

                            object? constraint = InvokeOptional(manager, "GetCurrentConstraintOnHandle", handle)
                                ?? InvokeOptional(manager, "GetPreferredConstraintOnHandle", handle);
                            if (constraint != null)
                            {
                                detail.ConstraintType = InvokeOptional(constraint, "GetConstraintType")?.ToString()
                                    ?? ReadOptionalMember(constraint, "ConstraintType")?.ToString()
                                    ?? string.Empty;
                                detail.TargetCount = ReadOptionalIntMember(constraint, "NumberOfTargets");
                                detail.TargetSummary = BuildConstraintTargetSummary(doc, constraint, detail.TargetCount);
                                if (constraint is RebarConstraint typedConstraint)
                                {
                                    detail.TargetHostFaceStableReferences = BuildConstraintTargetFaceStableReferences(
                                        doc,
                                        typedConstraint,
                                        detail.TargetCount);
                                    PopulateTargetRebarDetails(doc, typedConstraint, detail);
                                }
                                if (constraint is IDisposable disposableConstraint) disposableConstraint.Dispose();
                            }
                            item.Constraints.Add(detail);
                        }
                        catch (Exception ex)
                        {
                            item.Notes.Add("读取单个约束失败：" + ex.Message);
                        }
                        finally
                        {
                            try { handle.Dispose(); } catch { }
                        }
                    }

                    if (constrainedHandles.Count > MaxConstraintsPerBar)
                        item.Notes.Add($"约束明细只记录前 {MaxConstraintsPerBar} 个 constrained handles。");

                    if (handles != null)
                    {
                        foreach (RebarConstrainedHandle handle in handles)
                        {
                            if (constrainedHandles.Contains(handle)) continue;
                            try { handle.Dispose(); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                item.Notes.Add("约束管理器不可用：" + ex.Message);
            }
        }

        private static void PopulateTargetRebarDetails(
            Document doc,
            RebarConstraint constraint,
            RebarConstraintInspection detail)
        {
            RebarConstraintType type;
            try { type = constraint.GetConstraintType(); }
            catch { return; }
            if (type != RebarConstraintType.ToOtherRebar) return;

            int count = Math.Max(1, Math.Min(8, detail.TargetCount ?? 1));
            for (int index = 0; index < count; index++)
            {
                try
                {
                    Element? target = null;
                    try { target = constraint.GetTargetElement(index); }
                    catch { if (index == 0) target = constraint.GetTargetElement(); }
                    if (target is Autodesk.Revit.DB.Structure.Rebar targetRebar)
                    {
                        long id = targetRebar.Id.GetIdValue();
                        if (!detail.TargetRebarElementIds.Contains(id))
                            detail.TargetRebarElementIds.Add(id);
                        if (!string.IsNullOrWhiteSpace(targetRebar.UniqueId) &&
                            !detail.TargetRebarUniqueIds.Contains(targetRebar.UniqueId))
                            detail.TargetRebarUniqueIds.Add(targetRebar.UniqueId);
                    }
                }
                catch { }
            }

            try { detail.TargetRebarConstraintType = constraint.GetTargetRebarConstraintType().ToString(); }
            catch { }
            try
            {
                if (constraint.TargetRebarConstraintTypeIsEdge())
                    detail.TargetRebarEdgeNumber = constraint.GetTargetRebarEdgeNumber();
            }
            catch { }
            try
            {
                if (constraint.TargetIsBarBend())
                    detail.TargetRebarBendNumber = constraint.GetTargetRebarBendNumber();
            }
            catch { }
            try
            {
                if (constraint.TargetIsHookBend())
                    detail.TargetRebarHookBarEnd = constraint.GetTargetRebarHookBarEnd();
            }
            catch { }
            try { detail.DistanceToTargetRebarMm = ToMillimeters(constraint.GetDistanceToTargetRebar()); }
            catch { }
        }

        private static List<string> BuildConstraintTargetFaceStableReferences(
            Document doc,
            RebarConstraint constraint,
            int? targetCount)
        {
            var result = new List<string>();
            RebarConstraintType type;
            try { type = constraint.GetConstraintType(); }
            catch { return result; }

            if (type != RebarConstraintType.ToCover &&
                type != RebarConstraintType.FixedDistanceToHostFace)
                return result;

            int count = Math.Max(1, Math.Min(8, targetCount ?? 1));
            for (int index = 0; index < count; index++)
            {
                Reference? reference = null;
                try
                {
                    reference = constraint.GetTargetHostFaceReference(index);
                    string stable = reference.ConvertToStableRepresentation(doc);
                    if (!string.IsNullOrWhiteSpace(stable) && !result.Contains(stable))
                        result.Add(stable);
                }
                catch { }
                finally
                {
                    try { reference?.Dispose(); } catch { }
                }
            }
            return result;
        }

        private static string BuildConstraintTargetSummary(Document doc, object constraint, int? targetCount)
        {
            var targets = new List<string>();
            int count = Math.Max(1, Math.Min(8, targetCount ?? 1));
            for (int index = 0; index < count; index++)
            {
                object? target = InvokeOptional(constraint, "GetTargetElement", index);
                if (target == null && index == 0) target = InvokeOptional(constraint, "GetTargetElement");
                if (target is Element element)
                {
                    targets.Add($"{element.GetType().Name}#{element.Id.GetIdValue()}:{element.Name}");
                    continue;
                }
                if (target is ElementId id)
                {
                    Element? elementById = doc.GetElement(id);
                    targets.Add(elementById == null
                        ? $"ElementId#{id.GetIdValue()}"
                        : $"{elementById.GetType().Name}#{id.GetIdValue()}:{elementById.Name}");
                }
            }
            return string.Join(" | ", targets.Distinct());
        }

        private static void InspectSampleBarPositions(Document doc, Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            int count;
            try { count = Math.Max(0, rebar.NumberOfBarPositions); }
            catch { return; }
            if (count <= 0) return;

            var indexes = new SortedSet<int> { 0, count / 2, count - 1 };
            foreach (int index in indexes)
            {
                bool exists = DoesBarExist(rebar, index);
                var sample = new RebarBarPositionInspection { Index = index, Exists = exists };
                if (!exists)
                {
                    item.SampleBarPositions.Add(sample);
                    continue;
                }

                try
                {
                    IList<Curve> curves = GetActualPositionCenterlineCurves(rebar, index, out string geometryMode);
                    sample.GeometryMode = geometryMode;
                    sample.CenterlineLengthMm = Math.Round(curves.Sum(curve => ToMillimeters(curve.Length)), 3);
                    if (curves.Count > 0)
                    {
                        XYZ start = curves[0].GetEndPoint(0);
                        XYZ end = curves[curves.Count - 1].GetEndPoint(1);
                        sample.Start = ToPoint(start);
                        sample.End = ToPoint(end);
                        sample.Midpoint = ToPoint((start + end) * 0.5);
                    }
                }
                catch (Exception ex)
                {
                    item.Notes.Add($"读取 Bar Position {index} 实际几何失败：{ex.Message}");
                }

                // Free Form 的不同 bar position 可能匹配不同 shape；通过反射读取，旧版本不存在时忽略。
                if (rebar.IsRebarFreeForm())
                {
                    try
                    {
                        using (RebarFreeFormAccessor accessor = rebar.GetFreeFormAccessor())
                        {
                            object? shapeIdRaw = InvokeOptional(accessor, "GetShapeIdAtIndex", index);
                            if (shapeIdRaw is ElementId shapeId && shapeId != ElementId.InvalidElementId)
                            {
                                sample.ShapeId = shapeId.GetIdValue();
                                sample.ShapeName = doc.GetElement(shapeId)?.Name ?? string.Empty;
                            }
                        }
                    }
                    catch { }
                }

                item.SampleBarPositions.Add(sample);
            }
        }

        private static void InspectCurves(Autodesk.Revit.DB.Structure.Rebar rebar, RebarInspectionItem item)
        {
            try
            {
                int position = FindFirstExistingBarPosition(rebar);
                if (position < 0)
                {
                    item.Notes.Add("钢筋集中没有可读取的有效 Bar Position。");
                    return;
                }

                IList<Curve> curves = GetCenterlineCurves(rebar, position);
                int index = 0;
                foreach (Curve curve in curves.Take(MaxCurvesPerSample))
                {
                    var curveInfo = new RebarCurveInspection
                    {
                        Index = index++,
                        CurveType = curve.GetType().Name,
                        LengthMm = ToMillimeters(curve.Length)
                    };
                    try
                    {
                        curveInfo.Start = ToPoint(curve.GetEndPoint(0));
                        curveInfo.End = ToPoint(curve.GetEndPoint(1));
                    }
                    catch { }
                    item.CenterlineCurves.Add(curveInfo);
                }
            }
            catch (Exception ex)
            {
                item.Notes.Add("读取中心线失败：" + ex.Message);
            }
        }

        private static IList<Curve> GetCenterlineCurves(Autodesk.Revit.DB.Structure.Rebar rebar, int position)
        {
            return rebar.GetCenterlineCurves(
                false,
                false, // Revit 2026+ renamed this argument suppressHooksAndCranks.
                false,
                MultiplanarOption.IncludeAllMultiplanarCurves,
                position);
        }

        private static IList<Curve> GetActualPositionCenterlineCurves(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            int position,
            out string geometryMode)
        {
            // Autodesk documents GetTransformedCenterlineCurves as the API that applies both the
            // bar-position transform and the moved-bar transform. Invoke it reflectively so the
            // R00 inspector stays compile-compatible with the full 2021-2027 target matrix.
            try
            {
                MethodInfo? method = rebar.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(candidate =>
                        candidate.Name == "GetTransformedCenterlineCurves" &&
                        candidate.GetParameters().Length == 5);
                if (method != null)
                {
                    object? raw = method.Invoke(rebar, new object[]
                    {
                        false,
                        false,
                        false,
                        MultiplanarOption.IncludeAllMultiplanarCurves,
                        position
                    });
                    if (raw is IList<Curve> curves)
                    {
                        geometryMode = "TransformedCenterline";
                        return curves;
                    }
                    if (raw is IEnumerable<Curve> enumerable)
                    {
                        geometryMode = "TransformedCenterline";
                        return enumerable.ToList();
                    }
                }
            }
            catch { }

            geometryMode = "RawCenterlineFallback";
            return GetCenterlineCurves(rebar, position);
        }

        private static bool DoesBarExist(Autodesk.Revit.DB.Structure.Rebar rebar, int position)
        {
            object? value = InvokeOptional(rebar, "DoesBarExistAtPosition", position);
            return value is not bool exists || exists;
        }

        private static int FindFirstExistingBarPosition(Autodesk.Revit.DB.Structure.Rebar rebar)
        {
            int count;
            try { count = Math.Max(0, rebar.NumberOfBarPositions); }
            catch { return 0; }
            if (count == 0) return -1;

            for (int i = 0; i < count; i++)
            {
                if (DoesBarExist(rebar, i)) return i;
            }
            return -1;
        }

        private static double? ReadDiameterMillimeters(Element? barType)
        {
            if (barType == null) return null;
            foreach (string propertyName in new[] { "BarModelDiameter", "BarDiameter", "NominalDiameter" })
            {
                double? value = ReadOptionalDoubleMemberMillimeters(barType, propertyName);
                if (value.HasValue && value.Value > 0) return value;
            }
            return null;
        }

        private static long? ReadOptionalElementIdValue(object target, string methodName, params object[] args)
        {
            object? raw = InvokeOptional(target, methodName, args);
            if (raw is not ElementId id || id == ElementId.InvalidElementId) return null;
            return id.GetIdValue();
        }

        private static RebarPointInspection ToPoint(XYZ point)
        {
            return new RebarPointInspection
            {
                Xmm = ToMillimeters(point.X),
                Ymm = ToMillimeters(point.Y),
                Zmm = ToMillimeters(point.Z)
            };
        }

        private static double ToMillimeters(double internalValue)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), 3);
        }

        private static object? ReadOptionalMember(object target, string memberName)
        {
            try
            {
                PropertyInfo? property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null) return property.GetValue(target);
                return InvokeOptional(target, memberName);
            }
            catch { return null; }
        }

        private static bool? ReadOptionalBoolMember(object target, string memberName)
        {
            object? value = ReadOptionalMember(target, memberName);
            return value is bool flag ? flag : null;
        }

        private static int? ReadOptionalIntMember(object target, string memberName)
        {
            object? value = ReadOptionalMember(target, memberName);
            if (value is int intValue) return intValue;
            return null;
        }

        private static double? ReadOptionalDoubleMemberMillimeters(object target, string memberName)
        {
            object? raw = ReadOptionalMember(target, memberName);
            return raw is double value ? ToMillimeters(value) : null;
        }

        private static object? InvokeOptional(object target, string methodName, params object[] args)
        {
            try
            {
                Type[] types = args.Select(arg => arg.GetType()).ToArray();
                MethodInfo? method = target.GetType().GetMethod(methodName, types);
                if (method == null)
                {
                    method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .FirstOrDefault(candidate => candidate.Name == methodName &&
                            candidate.GetParameters().Length == args.Length);
                }
                return method?.Invoke(target, args);
            }
            catch { return null; }
        }

        private sealed class ElementIdComparer : IEqualityComparer<ElementId>
        {
            public bool Equals(ElementId? x, ElementId? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.GetIdValue() == y.GetIdValue();
            }

            public int GetHashCode(ElementId obj) => obj.GetIdValue().GetHashCode();
        }
    }
}
