using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "视图修改区", buttonText: "改比例保排版",
        tooltip: "调整所选图例的纸面间距、详图线长和文字位置。",
        largeIcon: "Icons/view_scale_layout_32.png", smallIcon: "Icons/view_scale_layout_16.png",
        usage: "使用：预选图例，填原排版比例和目标比例。若已改过视图比例，仍填写原排版比例。\n注意：详图构件只补偿插入位置，外形大小需核对。")]
    public sealed class ViewScaleLayoutCommand : IExternalCommand
    {
        private const string Title = "改比例保排版";
        private const double PositionTolerance = 1e-6;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;
            if (doc.IsFamilyDocument || doc.IsReadOnly || !SupportsView(view))
            {
                TaskDialog.Show(Title, "请在可编辑项目的平面、剖面、立面、详图或图例视图中使用。");
                return Result.Cancelled;
            }

            try
            {
                var selection = uiDoc.Selection.GetElementIds();
                if (selection.Count == 0)
                    selection = uiDoc.Selection.PickObjects(ObjectType.Element, "选择要保留排版的图例图元，完成后确认")
                        .Select(reference => reference.ElementId).Distinct().ToList();

                var items = selection.Select(id => doc.GetElement(id)).OfType<Element>().ToList();
                var notes = items.OfType<TextNote>().Where(note => CanEdit(note, view) && note.LeaderCount == 0).ToList();
                var curves = items.OfType<DetailCurve>().Where(curve => CanEdit(curve, view) &&
                    curve.GeometryCurve.IsBound && (curve.GeometryCurve is Line || curve.GeometryCurve is Arc)).ToList();
                var families = items.OfType<FamilyInstance>().Where(instance =>
                    CanEdit(instance, view) && instance.Location is LocationPoint &&
                    (IsCategory(instance, BuiltInCategory.OST_DetailComponents) ||
                     IsCategory(instance, BuiltInCategory.OST_GenericAnnotation))).ToList();

                // An existing join to a line outside the editable selection couples the two layouts.
                var curveIds = new HashSet<ElementId>(curves.Select(curve => curve.Id));
                var excluded = new List<DetailCurve>();
                while (true)
                {
                    var connected = curves.Where(curve => Adjacent(curve).Any(id => !curveIds.Contains(id))).ToList();
                    if (connected.Count == 0) break;
                    excluded.AddRange(connected);
                    foreach (var curve in connected) curveIds.Remove(curve.Id);
                    curves.RemoveAll(curve => !curveIds.Contains(curve.Id));
                }

                var editable = notes.Cast<Element>().Concat(curves).Concat(families).ToList();
                var editableIds = new HashSet<ElementId>(editable.Select(element => element.Id));
                if (editable.Count == 0)
                {
                    TaskDialog.Show(Title, "所选图元中没有可处理的独立文字、详图线或点式详图构件。");
                    return Result.Cancelled;
                }

                int originalViewScale = view.Scale;
                var dialog = new ViewScaleLayoutWindow(originalViewScale);
                new WindowInteropHelper(dialog).Owner = app.MainWindowHandle;
                if (dialog.ShowDialog() != true) return Result.Cancelled;
                if (dialog.OriginalScale == dialog.TargetScale)
                {
                    TaskDialog.Show(Title, "原排版比例与目标比例相同，无需补偿。");
                    return Result.Cancelled;
                }
                if (originalViewScale != dialog.OriginalScale && originalViewScale != dialog.TargetScale)
                {
                    TaskDialog.Show(Title, $"当前视图是 1:{originalViewScale}。请确保它与原排版比例或目标比例一致。");
                    return Result.Cancelled;
                }

                XYZ right = view.RightDirection.Normalize();
                XYZ up = view.UpDirection.Normalize();
                XYZ anchor;
                if (dialog.PickAnchor)
                {
                    Reference reference = uiDoc.Selection.PickObject(ObjectType.Element,
                        new AnchorFilter(editableIds), "点选一个已选文字、线或符号作为基准，例如图例标题");
                    anchor = AnchorOf(doc.GetElement(reference.ElementId));
                }
                else
                {
                    var points = editable.SelectMany(element => ExtentPoints(element, view)).ToList();
                    XYZ first = points[0];
                    anchor = first + right * points.Min(point => (point - first).DotProduct(right))
                                   + up * points.Max(point => (point - first).DotProduct(up));
                }

                double factor = (double)dialog.TargetScale / dialog.OriginalScale;
                XYZ Map(XYZ point)
                {
                    XYZ delta = point - anchor;
                    return point + (factor - 1) * (right * delta.DotProduct(right) + up * delta.DotProduct(up));
                }

                if (curves.Any(curve => curve.GeometryCurve.Length * factor <= doc.Application.ShortCurveTolerance))
                {
                    TaskDialog.Show(Title, "目标比例会让所选详图线短于 Revit 的最小长度。请检查选区或目标比例。");
                    return Result.Cancelled;
                }
                var targets = curves.ToDictionary(curve => curve.Id, curve =>
                {
                    Curve original = curve.GeometryCurve;
                    XYZ start = original.GetEndPoint(0);
                    Transform transform = Transform.Identity.ScaleBasis(factor);
                    transform.Origin = Map(start) - transform.OfVector(start);
                    return original.CreateTransformed(transform);
                });
                var noteTargets = notes.ToDictionary(note => note.Id, note => Map(note.Coord));
                var familyTargets = families.ToDictionary(instance => instance.Id,
                    instance => Map(((LocationPoint)instance.Location).Point));
                int details = families.Count(instance => IsCategory(instance, BuiltInCategory.OST_DetailComponents));
                var skipped = items.Where(item => !editableIds.Contains(item.Id)).ToList();
                string summary = $"原排版 1:{dialog.OriginalScale} → 目标 1:{dialog.TargetScale}；坐标补偿系数 {factor:0.####}。\n" +
                    $"文字 {notes.Count} 个、详图线 {curves.Count} 条、符号或构件 {families.Count} 个。";
                if (view.ViewType == ViewType.Legend && originalViewScale != dialog.TargetScale)
                    summary += "\n同一图例放在多张图纸上时，这些图纸都会显示更新后的图例。";
                if (details > 0)
                    summary += $"\n{details} 个详图构件只移动插入点，外形大小需出图核对。";
                if (skipped.Count > 0)
                    summary += $"\n跳过 {skipped.Count} 个图元，其中 {excluded.Count} 条详图线与选区外或无法处理的线相连。";

                var confirm = new TaskDialog(Title)
                {
                    MainInstruction = originalViewScale == dialog.TargetScale ? "按原比例补偿选区排版？" : "修改当前视图比例并补偿选区排版？",
                    MainContent = summary,
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (skipped.Count > 0)
                    confirm.ExpandedContent = "跳过的图元（含固定、组内、带引线或类型不支持的图元）\n" +
                        string.Join("\n", skipped.Select(item => $"{item.Id}  {item.Category?.Name ?? item.Name}"));
                if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                using var transaction = new Transaction(doc, Title);
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法开始排版调整。");
                if (originalViewScale != dialog.TargetScale) view.Scale = dialog.TargetScale;
                foreach (DetailCurve curve in curves) curve.SetGeometryCurve(targets[curve.Id], true);
                foreach (TextNote note in notes) note.Coord = noteTargets[note.Id];
                foreach (FamilyInstance family in families)
                {
                    XYZ offset = familyTargets[family.Id] - ((LocationPoint)family.Location).Point;
                    if (offset.GetLength() > PositionTolerance) ElementTransformUtils.MoveElement(doc, family.Id, offset);
                }
                doc.Regenerate();

                if (view.Scale != dialog.TargetScale)
                    throw new InvalidOperationException("目标视图比例未生效，请检查视图样板中的比例控制。");
                if (curves.Any(curve => !MatchesCurve(curve.GeometryCurve, targets[curve.Id]) ||
                                        Adjacent(curve).Any(id => !curveIds.Contains(id))) ||
                    notes.Any(note => note.Coord.DistanceTo(noteTargets[note.Id]) > PositionTolerance) ||
                    families.Any(family => ((LocationPoint)family.Location).Point.DistanceTo(familyTargets[family.Id]) > PositionTolerance))
                    throw new InvalidOperationException("图元受连接或约束影响，调整结果与预览不一致。");

                if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未提交排版调整。");
                TaskDialog.Show(Title, $"排版已调整，当前视图为 1:{view.Scale}。\n可用 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"排版调整失败：{ex.Message}";
                return Result.Failed;
            }
        }

        private static bool SupportsView(View view) =>
            view is ViewPlan || view is ViewSection || view.ViewType == ViewType.DraftingView || view.ViewType == ViewType.Legend;

        private static bool CanEdit(Element element, View view) =>
            element.OwnerViewId == view.Id && !element.Pinned && element.GroupId == ElementId.InvalidElementId;

        private static bool IsCategory(Element element, BuiltInCategory category) =>
            element.Category?.Id == new ElementId(category);

        private static IEnumerable<ElementId> Adjacent(DetailCurve curve) =>
            curve.GetAdjoinedCurveElements(0).Concat(curve.GetAdjoinedCurveElements(1));

        private static bool MatchesCurve(Curve actual, Curve expected)
        {
            for (int i = 0; i < 2; i++)
            {
                if (actual.GetEndPoint(0).DistanceTo(expected.GetEndPoint(i)) <= PositionTolerance &&
                    actual.GetEndPoint(1).DistanceTo(expected.GetEndPoint(1 - i)) <= PositionTolerance)
                    return actual.Evaluate(0.5, true).DistanceTo(expected.Evaluate(0.5, true)) <= PositionTolerance;
            }
            return false;
        }

        private static XYZ AnchorOf(Element element) => element switch
        {
            TextNote note => note.Coord,
            DetailCurve curve => curve.GeometryCurve.GetEndPoint(0),
            _ => ((LocationPoint)element.Location).Point
        };

        private static IEnumerable<XYZ> ExtentPoints(Element element, View view)
        {
            BoundingBoxXYZ? box = element.get_BoundingBox(view);
            if (box == null)
            {
                yield return AnchorOf(element);
                yield break;
            }
            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
                yield return box.Transform.OfPoint(new XYZ(x, y, z));
        }

        private sealed class AnchorFilter : ISelectionFilter
        {
            private readonly HashSet<ElementId> _ids;
            public AnchorFilter(HashSet<ElementId> ids) => _ids = ids;
            public bool AllowElement(Element element) => _ids.Contains(element.Id);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
