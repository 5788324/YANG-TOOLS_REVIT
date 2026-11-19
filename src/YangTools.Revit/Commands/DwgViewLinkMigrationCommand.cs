using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "2D修改", buttonText: "DWG转视图",
        tooltip: "按现有可见视图将 DWG 全模型链接转为视图专用链接。",
        largeIcon: "Icons/dwg_view_link_32.png", smallIcon: "Icons/dwg_view_link_16.png",
        usage: "使用：预选 DWG 链接或清空选择后点击按钮，查看要保留的视图，再确认转换。\n限制：位置或可见性无法核对时保留旧链接。")]
    public sealed class DwgViewLinkMigrationCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var app = commandData.Application;
            var uiDoc = app.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document.IsFamilyDocument || uiDoc.Document.IsReadOnly)
            {
                TaskDialog.Show("DWG 转视图", "请打开可编辑的 Revit 项目。");
                return Result.Cancelled;
            }

            var doc = uiDoc.Document;
            try
            {
                var selectedIds = uiDoc.Selection.GetElementIds();
                var plans = DwgViewLinkMigration.Scan(doc, selectedIds);
                if (plans.Count == 0)
                {
                    TaskDialog.Show("DWG 转视图", selectedIds.Count == 0
                        ? "项目中没有可扫描的 DWG 全模型链接。" : "预选范围内没有 DWG 全模型链接。");
                    return Result.Cancelled;
                }

                var ready = plans.Where(plan => plan.CanConvert).ToList();
                var preview = new StringBuilder();
                preview.AppendLine(selectedIds.Count > 0 ? "扫描范围为预选的 DWG。" : "扫描范围为当前项目的 DWG 全模型链接。");
                preview.AppendLine("转换成功后会移除对应的原全模型实例。可在 Revit 中撤销本批操作。");
                preview.AppendLine($"候选 {ready.Count} 个，保留原链接 {plans.Count - ready.Count} 个。");
                foreach (var plan in plans)
                {
                    preview.AppendLine();
                    preview.AppendLine(plan.Name);
                    foreach (string reason in plan.Blockers)
                        preview.AppendLine("  保留：" + reason);
                    foreach (View view in plan.Views)
                        preview.AppendLine("  视图：" + view.Name);
                }

                if (!ShowDetails(app, "DWG 转视图预览", preview.ToString(), ready.Count > 0))
                    return Result.Cancelled;

                int converted = 0;
                int created = 0;
                var failures = new StringBuilder();
                using (var group = new TransactionGroup(doc, "DWG 转视图专用链接"))
                {
                    if (group.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法开始 DWG 转换。");

                    foreach (var plan in ready)
                    {
                        try
                        {
                            created += DwgViewLinkMigration.Convert(doc, plan);
                            converted++;
                        }
                        catch (Exception ex)
                        {
                            failures.AppendLine($"{plan.Name}：{ex.Message}");
                            DiagnosticLog.WriteError("DWG 转视图：" + plan.Name, ex);
                        }
                    }

                    if (converted > 0)
                    {
                        if (group.Assimilate() != TransactionStatus.Committed)
                            throw new InvalidOperationException("批量转换未成功提交，请核对模型。");
                    }
                    else
                    {
                        group.RollBack();
                    }
                }

                var summary = $"转换 {converted} 个旧链接，创建 {created} 个视图专用链接。\n" +
                    $"保留原链接 {plans.Count - converted} 个，其中执行失败 {ready.Count - converted} 个。";
                if (failures.Length > 0)
                    summary += "\n\n" + failures;
                ShowDetails(app, "DWG 转视图结果", summary, false);
                return converted > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("DWG 转视图", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static bool ShowDetails(UIApplication app, string title, string content, bool canApply)
        {
            var layout = new DockPanel { Margin = new Thickness(14) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(buttons, Dock.Bottom);
            layout.Children.Add(buttons);
            var dialog = new Window
            {
                Title = title,
                Width = 700,
                Height = 490,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = layout
            };
            new WindowInteropHelper(dialog).Owner = app.MainWindowHandle;
            ThemeHelper.ApplyToWindow(dialog);

            if (canApply)
            {
                var apply = new Button { Content = "转换候选链接", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
                apply.Click += (_, _) => dialog.DialogResult = true;
                buttons.Children.Add(apply);
            }
            buttons.Children.Add(new Button { Content = "关闭", IsCancel = true, Padding = new Thickness(12, 5, 12, 5) });
            layout.Children.Add(new TextBox
            {
                Text = content,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 12)
            });
            return dialog.ShowDialog() == true;
        }
    }
}
