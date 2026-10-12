using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "导入工具区",
        "从CAD粘贴",
        "检测 AutoCAD Ctrl+C 生成的临时 DWG，并按当前设置导入到活动视图。",
        "Icons/paste_cad_32.png",
        "Icons/paste_cad_16.png")]
    public class PasteCadCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string? workingCopy = null;
            string operationId = Guid.NewGuid().ToString("N");
            Document? boundDocument = null;
            bool importCommitted = false;
            bool rollbackConfirmed = false;
            try
            {
                CadClipboardFileHelper.CleanupYangToolsCache(false, TimeSpan.FromDays(1));
                UIDocument? uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    TaskDialog.Show("从 CAD 粘贴", "请先打开一个 Revit 项目或族文档。 ");
                    return Result.Cancelled;
                }

                Document document = uiDocument.Document;
                boundDocument = document;
                if (document.IsReadOnly)
                {
                    TaskDialog.Show("从 CAD 粘贴", "当前文档为只读状态，无法导入 CAD。 ");
                    return Result.Cancelled;
                }

                View activeView = document.ActiveView;
                if (activeView == null || activeView.IsTemplate)
                {
                    TaskDialog.Show("从 CAD 粘贴", "当前没有可用的活动视图，或当前视图是视图样板。 ");
                    return Result.Cancelled;
                }

                bool supportsThisViewOnly = activeView.ViewType != ViewType.ThreeD;
                var window = new PasteCadWindow(activeView.Name, supportsThisViewOnly);
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                if (window.ShowDialog() != true)
                    return Result.Cancelled;
                // 模态窗口返回后重新检查文档/视图：禁止粘贴到已切换的目标。
                if (!ReferenceEquals(commandData.Application.ActiveUIDocument?.Document, document) ||
                    document.ActiveView == null || document.ActiveView.Id.GetIdValue() != activeView.Id.GetIdValue() || document.IsReadOnly)
                {
                    TaskDialog.Show("从 CAD 粘贴", "目标文档或活动视图已改变，已取消导入；请在目标视图中重新执行。");
                    return Result.Cancelled;
                }

                string? sourcePath = window.SelectedDwgPath;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    TaskDialog.Show("从 CAD 粘贴", "所选 DWG 文件不存在或已被系统清理。 ");
                    return Result.Failed;
                }

                workingCopy = CadClipboardFileHelper.CreateWorkingCopy(sourcePath);
                DiagnosticLog.WriteProductOperation(
                    "PasteCAD",
                    operationId,
                    "START",
                    document,
                    $"Path=IExternalCommand→PasteCadWindow.ShowDialog→Transaction→Document.Import; Source={sourcePath}; View={activeView.Name}; Placement={window.SelectedPlacement}; ThisViewOnly={supportsThisViewOnly && window.SelectedThisViewOnly}");
                DiagnosticLog.WriteInfo(
                    "从 CAD 粘贴",
                    $"Source={sourcePath}\nWorkingCopy={workingCopy}\nView={activeView.Name}\n" +
                    $"Unit={window.SelectedUnit}\nColor={window.SelectedColorMode}\nPlacement={window.SelectedPlacement}\n" +
                    $"ThisViewOnly={window.SelectedThisViewOnly}");

                ElementId importedId = ElementId.InvalidElementId;
                using (var transaction = new Transaction(document, "从 CAD 粘贴"))
                {
                    TransactionStatus startStatus = transaction.Start();
                    if (startStatus != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动 Revit 导入事务。 ");

                    try
                    {
                        using (var options = new DWGImportOptions
                        {
                            Unit = window.SelectedUnit,
                            ColorMode = window.SelectedColorMode,
                            Placement = window.SelectedPlacement,
                            ThisViewOnly = supportsThisViewOnly && window.SelectedThisViewOnly
                        })
                        {
                            bool imported;
                            try
                            {
                                imported = document.Import(workingCopy, options, activeView, out importedId);
                            }
                            catch (Exception importException) when (window.SelectedPlacement == ImportPlacement.Shared)
                            {
                                throw new InvalidOperationException(
                                    "按共享坐标导入失败。请确认当前 Revit 项目已经建立有效共享坐标，并确认该 DWG 的坐标基准与项目一致。" +
                                    " YangTools 不会自动获取/发布共享坐标，也不会失败后改用原点到原点。",
                                    importException);
                            }

                            if (!imported || importedId == ElementId.InvalidElementId)
                            {
                                string placementHint = window.SelectedPlacement == ImportPlacement.Shared
                                    ? "当前选择了“对齐共享坐标”。请确认 Revit 项目已建立有效共享坐标，且 DWG 坐标基准一致。"
                                    : "请确认文件完整、导入单位正确，并尝试在普通平面或三维视图中执行。";
                                throw new InvalidOperationException("Revit 未能导入该 DWG。\n\n" + placementHint);
                            }
                        }

                        TransactionStatus commitStatus = transaction.Commit();
                        if (commitStatus != TransactionStatus.Committed)
                            throw new InvalidOperationException($"导入事务未提交，状态：{commitStatus}");
                        importCommitted = true;
                    }
                    catch
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                        {
                            TransactionStatus rollbackStatus = transaction.RollBack();
                            rollbackConfirmed = rollbackStatus == TransactionStatus.RolledBack;
                        }
                        else
                        {
                            rollbackConfirmed = transaction.GetStatus() == TransactionStatus.RolledBack;
                        }
                        throw;
                    }
                }

                // 导入事务已提交；之后的回读/选中失败不能误报为“事务已回滚”。
                if (!(document.GetElement(importedId) is ImportInstance))
                {
                    DiagnosticLog.WriteProductOperation("PasteCAD", operationId, "COMMITTED_READBACK_UNCONFIRMED", document,
                        $"ImportInstanceId={importedId.GetIdValue()}; 无法读取新导入实例");
                    TaskDialog.Show("从 CAD 粘贴", "导入事务已提交，但未能回读新 CAD 实例。请检查项目中的导入结果，勿立即重复操作。");
                    return Result.Succeeded;
                }

                bool selectionRefreshed = true;
                try
                {
                    uiDocument.Selection.SetElementIds(new List<ElementId> { importedId });
                    uiDocument.RefreshActiveView();
                }
                catch (Exception ex)
                {
                    selectionRefreshed = false;
                    DiagnosticLog.WriteError("选中新导入 CAD", ex);
                }

                DiagnosticLog.WriteProductOperation(
                    "PasteCAD",
                    operationId,
                    "COMMITTED",
                    document,
                    $"ImportInstanceId={importedId.GetIdValue()}; Placement={window.SelectedPlacement}; Source={sourcePath}; ViewId={activeView.Id.GetIdValue()}");

                TaskDialog.Show(
                    "从 CAD 粘贴",
                    $"CAD 导入完成。\n\n" +
                    $"目标视图：{activeView.Name}\n" +
                    $"导入实例 ID：{importedId}\n" +
                    $"来源文件：{Path.GetFileName(sourcePath)}\n" +
                    $"定位：{GetPlacementDisplayName(window.SelectedPlacement)}\n" +
                    $"范围：{(supportsThisViewOnly && window.SelectedThisViewOnly ? "仅当前视图" : "模型级")}\n\n" +
                    (selectionRefreshed
                        ? "已选中新导入实例，可使用一次 Ctrl+Z 撤销。"
                        : "CAD 已提交，但界面选中/刷新失败；请手动检查结果，可使用一次 Ctrl+Z 撤销。"));
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteProductOperation("PasteCAD", operationId,
                    importCommitted ? "COMMITTED_POSTPROCESS_ERROR" : (rollbackConfirmed ? "FAILED_ROLLEDBACK" : "FAILED_ROLLBACK_UNCONFIRMED"),
                    boundDocument, ex.ToString());
                string logPath = DiagnosticLog.WriteError("从 CAD 粘贴", ex);
                message = ex.Message;
                TaskDialog.Show(
                    "从 CAD 粘贴",
                    (importCommitted ? "CAD 导入事务已提交，但后续操作失败；不要重复导入。" :
                     rollbackConfirmed ? "CAD 导入失败，事务已确认回滚。" :
                     "CAD 导入失败，事务回滚状态无法确认，请检查项目后再重试。") +
                    $"\n\n{ex.Message}" +
                    (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n\n诊断日志：{logPath}"));
                return importCommitted ? Result.Succeeded : Result.Failed;
            }
            finally
            {
                CadClipboardFileHelper.CleanupWorkingCopy(workingCopy);
            }
        }

        private static string GetPlacementDisplayName(ImportPlacement placement)
        {
            return placement switch
            {
                ImportPlacement.Centered => "中心到中心",
                ImportPlacement.Shared => "对齐共享坐标",
                _ => "原点到原点"
            };
        }
    }
}
