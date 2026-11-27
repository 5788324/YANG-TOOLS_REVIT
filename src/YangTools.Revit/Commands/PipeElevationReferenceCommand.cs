using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "模型修改区", buttonText: "管道高程",
        tooltip: "保持当前管道标高数值不变，将管中心基准转换为管内底、管外底或管外顶。",
        largeIcon: "Icons/pipe_elevation_ref_32.png", smallIcon: "Icons/pipe_elevation_ref_16.png",
        usage: "使用：框选或运行后点选管道，选择目标高程基准（管内底/管外底/管外顶），点击应用。\n注意：自动跳过立管、固定及组内管道；弯头等管件连接能保留则保留，无法保留时自动断开或删除阻挡管件，支持单次 Ctrl+Z 撤销。")]
    public sealed class PipeElevationReferenceCommand : IExternalCommand
    {
        private const string Title = "管道高程";
        private const double MmPerFoot = 304.8;
        private const double MoveVerifyToleranceFeet = 0.2 / MmPerFoot;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            if (doc.IsFamilyDocument || doc.IsReadOnly || doc.IsModifiable)
            {
                TaskDialog.Show(Title, "请在可编辑的项目文档中运行此命令。");
                return Result.Cancelled;
            }

            try
            {
                List<Pipe> selectedPipes = uiDoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .OfType<Pipe>()
                    .ToList();

                if (selectedPipes.Count == 0)
                {
                    IList<Reference> picked = uiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        new PipeSelectionFilter(),
                        "选择要转换高程基准的管道，点击完成");
                    selectedPipes = picked
                        .Select(r => r.ElementId)
                        .Distinct()
                        .Select(doc.GetElement)
                        .OfType<Pipe>()
                        .ToList();
                }

                if (selectedPipes.Count == 0) return Result.Cancelled;

                var adjustable = new List<Pipe>();
                int skippedCount = 0;
                foreach (Pipe pipe in selectedPipes)
                {
                    if (!CanAdjustPipe(pipe))
                    {
                        skippedCount++;
                        continue;
                    }
                    adjustable.Add(pipe);
                }

                if (adjustable.Count == 0)
                {
                    TaskDialog.Show(Title, "所选管道均为立管、已固定或属于模型组，无可调整对象。");
                    return Result.Cancelled;
                }

                int diameterCount = adjustable
                    .Select(p => Math.Round(GetNominalDiameterFeet(p) * MmPerFoot, 1))
                    .Distinct()
                    .Count();

                var dialog = new PipeElevationReferenceWindow(adjustable.Count, diameterCount, skippedCount);
                new WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;
                if (dialog.ShowDialog() != true) return Result.Cancelled;

                var pipeDeltas = new Dictionary<ElementId, double>();
                var originalMidZ = new Dictionary<ElementId, double>();
                foreach (Pipe pipe in adjustable)
                {
                    double dz = ComputeDeltaZFeet(pipe, dialog.TargetReference, dialog.IncludeInsulation);
                    if (Math.Abs(dz) < 1e-6) continue;
                    pipeDeltas[pipe.Id] = dz;
                    originalMidZ[pipe.Id] = GetPipeMidZ(pipe);
                }

                if (pipeDeltas.Count == 0)
                {
                    TaskDialog.Show(Title, "所选管道无需调整高程。");
                    return Result.Succeeded;
                }

                int movedCount = 0;
                int deletedFittingsCount = 0;
                int failedCount = 0;

                using var tg = new TransactionGroup(doc, Title);
                if (tg.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动管道高程事务组。");

                // Pass 1: Move pipes grouped by identical deltaZ together with shared fittings to preserve elbows where possible.
                var deltaGroups = pipeDeltas
                    .GroupBy(kv => Math.Round(kv.Value * MmPerFoot, 2))
                    .ToList();

                var movedIds = new HashSet<ElementId>();
                foreach (var grp in deltaGroups)
                {
                    double dz = grp.First().Value;
                    var groupPipeIds = new HashSet<ElementId>(grp.Select(x => x.Key));
                    var moveSet = new HashSet<ElementId>(groupPipeIds);

                    foreach (ElementId pid in groupPipeIds)
                    {
                        if (doc.GetElement(pid) is not Pipe p) continue;
                        foreach (ElementId fittingId in CollectSharedPipeFittings(p, groupPipeIds))
                            moveSet.Add(fittingId);
                    }

                    using var tx = new Transaction(doc, "批量平移管道与管件");
                    ConfigureWarningSuppressor(tx);
                    if (tx.Start() == TransactionStatus.Started)
                    {
                        try
                        {
                            ElementTransformUtils.MoveElements(doc, moveSet.ToList(), new XYZ(0, 0, dz));
                            doc.Regenerate();
                            if (tx.Commit() == TransactionStatus.Committed)
                            {
                                foreach (ElementId pid in groupPipeIds)
                                {
                                    if (doc.GetElement(pid) is Pipe p &&
                                        Math.Abs(GetPipeMidZ(p) - (originalMidZ[pid] + dz)) <= MoveVerifyToleranceFeet)
                                    {
                                        movedIds.Add(pid);
                                        movedCount++;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                        }
                    }
                }

                // Pass 2: For any pipe still at its original Z (blocked by riser/tee/different-size elbow),
                // disconnect connectors (and optionally delete blocking pipe fittings), then force-apply elevation.
                var remainingIds = pipeDeltas.Keys.Where(id => !movedIds.Contains(id)).ToList();
                if (remainingIds.Count > 0)
                {
                    using var tx = new Transaction(doc, "断开冲突管件并调整管道高程");
                    ConfigureWarningSuppressor(tx);
                    if (tx.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动冲突管道调整事务。");

                    var fittingsToDelete = new HashSet<ElementId>();
                    foreach (ElementId pid in remainingIds)
                    {
                        if (doc.GetElement(pid) is not Pipe pipe || !pipe.IsValidObject)
                        {
                            failedCount++;
                            continue;
                        }

                        DisconnectPipeAndCollectBlockingFittings(pipe, dialog.DeleteBlockingFittings, fittingsToDelete);
                    }

                    foreach (ElementId fid in fittingsToDelete)
                    {
                        Element? fitting = doc.GetElement(fid);
                        if (fitting != null && fitting.IsValidObject && !fitting.Pinned && fitting.GroupId == ElementId.InvalidElementId)
                        {
                            try
                            {
                                doc.Delete(fid);
                                deletedFittingsCount++;
                            }
                            catch
                            {
                                // Ignore if already cascade-deleted.
                            }
                        }
                    }

                    doc.Regenerate();

                    foreach (ElementId pid in remainingIds)
                    {
                        if (doc.GetElement(pid) is not Pipe pipe || !pipe.IsValidObject)
                        {
                            failedCount++;
                            continue;
                        }

                        double targetMidZ = originalMidZ[pid] + pipeDeltas[pid];
                        double currentMidZ = GetPipeMidZ(pipe);
                        double remainingDz = targetMidZ - currentMidZ;
                        if (Math.Abs(remainingDz) <= MoveVerifyToleranceFeet)
                        {
                            movedCount++;
                            continue;
                        }

                        if (TryApplyPipeDeltaZ(doc, pipe, remainingDz))
                        {
                            doc.Regenerate();
                            if (Math.Abs(GetPipeMidZ(pipe) - targetMidZ) <= MoveVerifyToleranceFeet)
                                movedCount++;
                            else
                                failedCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }

                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit 未提交冲突管道高程修改。");
                }

                if (tg.Assimilate() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未合并管道高程事务组。");

                string refLabel = dialog.TargetReference switch
                {
                    PipeElevationTargetReference.Invert => "管内底",
                    PipeElevationTargetReference.Bottom => "管外底",
                    _ => "管外顶"
                };

                var parts = new List<string> { $"已将 {movedCount} 根管道的中心数值转换为「{refLabel}」。" };
                if (deletedFittingsCount > 0) parts.Add($"已移除 {deletedFittingsCount} 个无法保留连接的阻挡管件。");
                if (skippedCount > 0) parts.Add($"跳过 {skippedCount} 根立管/固定/组内管道。");
                if (failedCount > 0) parts.Add($"另有 {failedCount} 根管道因模型约束未能移动。");
                TaskDialog.Show(Title, string.Join("\n", parts));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"管道高程调整失败：{ex.Message}";
                return Result.Failed;
            }
        }

        private static bool CanAdjustPipe(Pipe pipe)
        {
            if (!pipe.IsValidObject || pipe.Pinned || pipe.GroupId != ElementId.InvalidElementId)
                return false;
            if (pipe.Location is not LocationCurve lc || lc.Curve is not Line line)
                return false;
            XYZ dir = line.Direction;
            if (Math.Abs(dir.Z) > 0.99) return false; // Vertical riser
            return true;
        }

        private static double GetNominalDiameterFeet(Pipe pipe)
        {
            return pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0.0;
        }

        private static double ComputeDeltaZFeet(Pipe pipe, PipeElevationTargetReference target, bool includeInsulation)
        {
            double nominal = GetNominalDiameterFeet(pipe);
            double inner = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_INNER_DIAM_PARAM)?.AsDouble() ?? 0.0;
            double outer = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)?.AsDouble() ?? 0.0;
            if (inner <= 1e-6) inner = nominal;
            if (outer <= 1e-6) outer = nominal;

            double insul = includeInsulation
                ? (pipe.get_Parameter(BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS)?.AsDouble() ?? 0.0)
                : 0.0;

            return target switch
            {
                PipeElevationTargetReference.Invert => inner * 0.5,
                PipeElevationTargetReference.Bottom => (outer * 0.5) + insul,
                PipeElevationTargetReference.Top => -((outer * 0.5) + insul),
                _ => 0.0
            };
        }

        private static double GetPipeMidZ(Pipe pipe)
        {
            var curve = ((LocationCurve)pipe.Location).Curve;
            return 0.5 * (curve.GetEndPoint(0).Z + curve.GetEndPoint(1).Z);
        }

        private static IEnumerable<ElementId> CollectSharedPipeFittings(Pipe pipe, HashSet<ElementId> groupPipeIds)
        {
            ConnectorManager? cm = pipe.ConnectorManager;
            if (cm == null) yield break;

            foreach (Connector conn in cm.Connectors)
            {
                if (conn.Domain != Domain.DomainPiping || !conn.IsConnected) continue;
                foreach (Connector other in conn.AllRefs)
                {
                    Element? owner = other.Owner;
                    if (owner == null || owner.Id == pipe.Id || !IsPipeFitting(owner)) continue;
                    if (owner.Pinned || owner.GroupId != ElementId.InvalidElementId) continue;
                    if (AreAllConnectedPipesInGroup(owner, groupPipeIds))
                        yield return owner.Id;
                }
            }
        }

        private static bool AreAllConnectedPipesInGroup(Element fitting, HashSet<ElementId> groupPipeIds)
        {
            if (fitting is not FamilyInstance fi || fi.MEPModel?.ConnectorManager == null)
                return false;

            bool hasPipe = false;
            foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
            {
                if (c.Domain != Domain.DomainPiping || !c.IsConnected) continue;
                foreach (Connector r in c.AllRefs)
                {
                    if (r.Owner == null || r.Owner.Id == fitting.Id) continue;
                    if (r.Owner is Pipe p)
                    {
                        hasPipe = true;
                        if (!groupPipeIds.Contains(p.Id)) return false;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return hasPipe;
        }

        private static void DisconnectPipeAndCollectBlockingFittings(Pipe pipe, bool collectFittings, HashSet<ElementId> fittingsToDelete)
        {
            ConnectorManager? cm = pipe.ConnectorManager;
            if (cm == null) return;

            foreach (Connector conn in cm.Connectors)
            {
                if (conn.Domain != Domain.DomainPiping || !conn.IsConnected) continue;
                var connectedRefs = new List<Connector>();
                foreach (Connector other in conn.AllRefs)
                {
                    if (other.Owner != null && other.Owner.Id != pipe.Id)
                        connectedRefs.Add(other);
                }

                foreach (Connector other in connectedRefs)
                {
                    try { conn.DisconnectFrom(other); } catch { }
                    if (collectFittings && other.Owner != null && IsPipeFitting(other.Owner))
                        fittingsToDelete.Add(other.Owner.Id);
                }
            }
        }

        private static bool TryApplyPipeDeltaZ(Document doc, Pipe pipe, double dz)
        {
            try
            {
                Parameter? startParam = pipe.get_Parameter(BuiltInParameter.RBS_START_OFFSET_PARAM);
                Parameter? endParam = pipe.get_Parameter(BuiltInParameter.RBS_END_OFFSET_PARAM);
                if (startParam != null && !startParam.IsReadOnly && endParam != null && !endParam.IsReadOnly)
                {
                    double s0 = startParam.AsDouble();
                    double e0 = endParam.AsDouble();
                    if (Math.Abs(s0 - e0) > 1e-6)
                    {
                        startParam.Set(s0 + dz);
                        endParam.Set(e0 + dz);
                        return true;
                    }
                }

                Parameter? midParam = pipe.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
                if (midParam != null && !midParam.IsReadOnly)
                {
                    midParam.Set(midParam.AsDouble() + dz);
                    return true;
                }

                ElementTransformUtils.MoveElement(doc, pipe.Id, new XYZ(0, 0, dz));
                return true;
            }
            catch
            {
                try
                {
                    ElementTransformUtils.MoveElement(doc, pipe.Id, new XYZ(0, 0, dz));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool IsPipeFitting(Element element)
        {
            if (element.Category == null) return false;
#if REVIT2024_OR_GREATER
            return element.Category.Id.Value == (long)BuiltInCategory.OST_PipeFitting;
#else
            return element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting;
#endif
        }

        private static void ConfigureWarningSuppressor(Transaction tx)
        {
            FailureHandlingOptions opts = tx.GetFailureHandlingOptions();
            opts.SetFailuresPreprocessor(new SilentWarningPreprocessor());
            tx.SetFailureHandlingOptions(opts);
        }

        private sealed class PipeSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Pipe;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }

        private sealed class SilentWarningPreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (FailureMessageAccessor failure in failuresAccessor.GetFailureMessages())
                {
                    if (failure.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(failure);
                }
                return FailureProcessingResult.Continue;
            }
        }
    }
}
