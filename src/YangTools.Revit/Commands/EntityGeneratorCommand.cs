using System;
using System.Linq;
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
        panelName: "模型修改区", buttonText: "实体生成",
        tooltip: "由轮廓生成放样或拉伸实体。",
        largeIcon: "Icons/entity_generator_32.png", smallIcon: "Icons/entity_generator_16.png",
        usage: "使用：点击按钮，选取生成所需的轮廓线或几何，在窗口设置生成方式及相关标高。\n注意：执行前核对轮廓闭合、标高和偏移。")]
    public class EntityGeneratorCommand : IExternalCommand
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
                var state = new EntityGeneratorState
                {
                    TemplatePath = settings.EntityGeneratorTemplatePath,
                    FamilyName = settings.EntityGeneratorFamilyName,
                    MaterialName = settings.EntityGeneratorMaterialName,
                    TargetLevelName = settings.EntityGeneratorLevelName,
                    IsVoid = settings.EntityGeneratorIsVoid,
                    CutWithVoids = settings.EntityGeneratorCutWithVoids
                };
                if (settings.EntityGeneratorCategoryValue.HasValue &&
                    BuiltInCategoryHelper.TryFromInt64(settings.EntityGeneratorCategoryValue.Value, out BuiltInCategory storedCategory))
                {
                    state.TargetCategory = storedCategory;
                }

                while (true)
                {
                    var window = new EntityGeneratorWindow(uiApp, state);
                    var helper = new System.Windows.Interop.WindowInteropHelper(window)
                    {
                        Owner = uiApp.MainWindowHandle
                    };

                    DiagnosticLog.WriteInfo("实体生成命令", "打开参数窗口");
                    bool? dialogResult = window.ShowDialog();
                    state = window.State;
                    DiagnosticLog.WriteInfo(
                        "实体生成命令",
                        $"参数窗口关闭：Result={dialogResult}; Action={window.Action}; Profiles={state.Profiles.Count}");

                    if (window.Action == GeneratorDialogAction.SelectInput)
                    {
                        try
                        {
                            var references = uiDoc.Selection.PickObjects(
                                ObjectType.Element,
                                new ProfileFamilyInstanceFilter(),
                                "请选择至少两个包含闭合模型线轮廓的族实例");

                            state.Profiles = references
                                .Select(reference => uiDoc.Document.GetElement(reference) as FamilyInstance)
                                .Where(instance => instance != null)
                                .Cast<FamilyInstance>()
                                .ToList();

                            DiagnosticLog.WriteInfo(
                                "实体生成命令",
                                $"已选择轮廓：Count={state.Profiles.Count}; IDs={string.Join(",", state.Profiles.Select(item => item.Id.ToString()))}");

                            if (state.Profiles.Count < 2)
                            {
                                TaskDialog.Show("提示", "实体生成至少需要两个有效的族实例轮廓，请重新选择。");
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                            DiagnosticLog.WriteInfo("实体生成命令", "用户取消轮廓选择，返回参数窗口");
                        }

                        continue;
                    }

                    if (window.Action != GeneratorDialogAction.Generate || dialogResult != true)
                        return Result.Cancelled;

                    if (state.Profiles.Count < 2)
                        throw new InvalidOperationException("请至少选择两个轮廓实例进行放样。");

                    DiagnosticLog.WriteInfo(
                        "实体生成命令",
                        $"开始引擎：Profiles={state.Profiles.Count}; IDs={string.Join(",", state.Profiles.Select(item => item.Id.ToString()))}");

                    EntityGeneratorEngine.Execute(uiApp, uiDoc.Document, state);
                    DiagnosticLog.WriteInfo("实体生成命令", "引擎执行成功");
                    TaskDialog.Show("完成", "Loft 实体族已生成并载入项目。");
                    return Result.Succeeded;
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("实体生成(Loft)", ex);
                TaskDialog.Show(
                    "实体生成失败",
                    $"Loft 生成未能确认完成；如载入或事务已经提交，请检查项目模型再重试。\n\n错误信息：{ex.Message}\n\n诊断日志：{(string.IsNullOrEmpty(logPath) ? "日志写入失败" : logPath)}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private sealed class ProfileFamilyInstanceFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is FamilyInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
