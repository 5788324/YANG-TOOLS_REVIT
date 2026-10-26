using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "模型修改区", buttonText: "实体提取",
        tooltip: "从图元几何提取实体并生成族对象。",
        largeIcon: "Icons/solid_extract_32.png", smallIcon: "Icons/solid_extract_16.png",
        usage: "使用：点击按钮，按提示选择源图元，在窗口配置目标族与生成位置后执行。\n注意：源几何的宿主/标高与偏移需要执行后核对。")]
    public class SolidGeneratorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument? uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    TaskDialog.Show("提示", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }

                UserSettings settings = UserSettings.Load();
                var state = new SolidGeneratorState
                {
                    TemplatePath = string.IsNullOrWhiteSpace(settings.SolidGeneratorTemplatePath)
                        ? settings.EntityGeneratorTemplatePath
                        : settings.SolidGeneratorTemplatePath,
                    FamilyName = settings.SolidGeneratorFamilyName,
                    MaterialName = settings.SolidGeneratorMaterialName,
                    TargetLevelName = settings.SolidGeneratorLevelName
                };
                if (settings.SolidGeneratorCategoryValue.HasValue &&
                    BuiltInCategoryHelper.TryFromInt64(settings.SolidGeneratorCategoryValue.Value, out BuiltInCategory storedCategory))
                {
                    state.TargetCategory = storedCategory;
                }

                while (true)
                {
                    var window = new SolidGeneratorWindow(uiApp, state);
                    var helper = new System.Windows.Interop.WindowInteropHelper(window)
                    {
                        Owner = uiApp.MainWindowHandle
                    };

                    DiagnosticLog.WriteInfo("实体提取命令", "打开参数窗口");
                    bool? dialogResult = window.ShowDialog();
                    state = window.State;
                    DiagnosticLog.WriteInfo(
                        "实体提取命令",
                        $"参数窗口关闭：Result={dialogResult}; Action={window.Action}; SourceId={state.SourceElementId}");

                    if (window.Action == GeneratorDialogAction.SelectInput)
                    {
                        try
                        {
                            Reference reference = uiDoc.Selection.PickObject(
                                ObjectType.Element,
                                "请选择要提取几何体的图元");
                            state.SourceElementId = reference.ElementId;
                            DiagnosticLog.WriteInfo("实体提取命令", $"已选择源图元：Id={state.SourceElementId}");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            DiagnosticLog.WriteInfo("实体提取命令", "用户取消源图元选择，返回参数窗口");
                        }

                        continue;
                    }

                    if (window.Action != GeneratorDialogAction.Generate || dialogResult != true)
                        return Result.Cancelled;

                    if (uiApp.ActiveUIDocument == null ||
                        !DocumentIdentityGuard.IsSameActiveDocument(uiApp, uiDoc.Document))
                        throw new InvalidOperationException("当前 Revit 项目已切换，请重新打开实体提取命令，避免操作错误文档。");
                    if (state.SourceElementId == ElementId.InvalidElementId)
                        throw new InvalidOperationException("没有有效的源图元。");

                    DiagnosticLog.WriteInfo("实体提取命令", $"开始引擎：SourceId={state.SourceElementId}");
                    SolidGeneratorEngine.Execute(uiApp, uiDoc.Document, state);
                    DiagnosticLog.WriteInfo("实体提取命令", "引擎执行成功");
                    TaskDialog.Show("完成", "实体族已生成并载入项目。");
                    return Result.Succeeded;
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("实体提取", ex);
                TaskDialog.Show(
                    "实体提取失败",
                    $"实体提取未能确认完成。项目是否全部回滚取决于事务返回状态；如有回滚状态未知提示，请先检查模型再重试。\n\n错误信息：{ex.Message}\n\n诊断日志：{(string.IsNullOrEmpty(logPath) ? "日志写入失败" : logPath)}");
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
