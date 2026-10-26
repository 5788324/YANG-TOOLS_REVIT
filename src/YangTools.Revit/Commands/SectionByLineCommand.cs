using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    public class LineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem is CurveElement curveElem)
            {
                return curveElem.GeometryCurve is Line;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "视图修改区", buttonText: "沿线剖面",
        tooltip: "根据选中的模型线或详图线创建剖面视图。",
        largeIcon: "Icons/section_line_32.png", smallIcon: "Icons/section_line_16.png",
        usage: "使用：点击按钮并按提示拾取可用直线，按要求确认剖面方向与生成选项。\n注意：线段太短或方向不合法时无法创建。")]
    public class SectionByLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("提示", "请先打开一个项目文档。");
                return Result.Cancelled;
            }
            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument || doc.IsReadOnly)
            {
                TaskDialog.Show("创建剖面", "此功能需要可修改的 Revit 项目文档，不支持族编辑器或只读项目。");
                return Result.Cancelled;
            }

            try
            {
                Reference selRef = uidoc.Selection.PickObject(ObjectType.Element, new LineSelectionFilter(), "请选择一根模型线或详图线");
                if (selRef == null) return Result.Cancelled;

                CurveElement curveElement = doc.GetElement(selRef) as CurveElement;
                if (curveElement == null) return Result.Cancelled;

                Line line = curveElement.GeometryCurve as Line;
                if (line == null)
                {
                    TaskDialog.Show("错误", "所选图元不是直线。");
                    return Result.Failed;
                }

                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);
                double length = p0.DistanceTo(p1);
                if (double.IsNaN(length) || double.IsInfinity(length) ||
                    length <= doc.Application.ShortCurveTolerance)
                    throw new InvalidOperationException("所选线过短，无法创建稳定的剖面范围。");

                XYZ dir = (p1 - p0).Normalize();
                // 恢复原点为中心点
                XYZ origin = (p0 + p1) / 2.0;

                XYZ basisX = dir;
                XYZ basisY;
                XYZ basisZ;

                // 判断线是否几乎垂直
                if (Math.Abs(dir.DotProduct(XYZ.BasisZ)) > 0.99)
                {
                    // 垂直线，利用当前视图的右方向来推导剖面的深度方向
                    XYZ viewRight = doc.ActiveView.RightDirection;
                    XYZ depth = dir.CrossProduct(viewRight);
                    // 不允许先 Normalize 再判断零向量；这种情况会先抛异常。
                    if (depth.IsZeroLength()) depth = dir.CrossProduct(XYZ.BasisX);
                    if (depth.IsZeroLength()) depth = dir.CrossProduct(XYZ.BasisY);
                    if (depth.IsZeroLength())
                        throw new InvalidOperationException("无法从线与当前视图推导剖面方向。");
                    basisZ = depth.Normalize();
                    basisY = basisZ.CrossProduct(dir).Normalize();
                }
                else
                {
                    // 水平或倾斜线，强制剖面向上方向为全局 Z
                    basisZ = dir.CrossProduct(XYZ.BasisZ).Normalize();
                    basisY = basisZ.CrossProduct(dir).Normalize();
                }

                Transform t = Transform.Identity;
                t.Origin = origin;
                t.BasisX = basisX;
                t.BasisY = basisY;
                t.BasisZ = basisZ;

                BoundingBoxXYZ bbox = new BoundingBoxXYZ();
                bbox.Transform = t;

                double halfLength = length / 2.0;
                // X 范围：-L/2 到 L/2 (宽度为 L，原点在中点，完美覆盖 p0 到 p1)
                // Y 范围：-L/2 到 L/2 (高度为 L，以线为中心上下均分)
                // Z 范围：0 到 L (根据用户反馈修改方向)
                bbox.Min = new XYZ(-halfLength, -halfLength, 0);
                bbox.Max = new XYZ(halfLength, halfLength, length);

                // 寻找“剖面”类型的视图族
                ViewFamilyType sectionType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

                if (sectionType == null)
                {
                    TaskDialog.Show("错误", "当前项目没有剖面类型的视图族！");
                    return Result.Failed;
                }

                ElementId sectionId;
                using (Transaction trans = new Transaction(doc, "创建剖面(By Line)"))
                {
                    if (trans.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("创建剖面事务未能启动。");
                    try
                    {
                        ViewSection newSection = ViewSection.CreateSection(doc, sectionType.Id, bbox)
                            ?? throw new InvalidOperationException("Revit 未返回有效剖面视图。");
                        sectionId = newSection.Id;
                        TransactionStatus status = trans.Commit();
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException("剖面创建事务未提交，状态：" + status);
                    }
                    catch
                    {
                        if (trans.GetStatus() == TransactionStatus.Started)
                        {
                            TransactionStatus status = trans.RollBack();
                            if (status != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("剖面创建失败且回滚状态无法确认，请检查模型。");
                        }
                        throw;
                    }
                }

                // Commit 之后的回读失败是独立问题，不能说前面的事务已回滚。
                bool readbackConfirmed;
                try { readbackConfirmed = doc.GetElement(sectionId) is ViewSection; }
                catch { readbackConfirmed = false; }
                if (!readbackConfirmed)
                    TaskDialog.Show("创建剖面", "剖面创建事务已提交，但未能回读剖面视图，请在项目浏览器中检查，勿重复执行。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", "生成剖面失败: " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
