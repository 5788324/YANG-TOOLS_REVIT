using System;
using System.Collections.Generic;
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
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "2D修改", buttonText: "DWG转视图",
        tooltip: "按现有可见视图将 DWG 全模型链接转为视图专用链接。",
        largeIcon: "Icons/dwg_view_link_32.png", smallIcon: "Icons/dwg_view_link_16.png",
        usage: "使用：预选 DWG 链接或清空选择后点击按钮，在预览中勾选要转换的链接。\n限制：位置或可见性无法核对时保留旧链接。")]
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

                var checkedPlans = new List<DwgViewLinkMigration.Plan>();
                var scope = selectedIds.Count > 0 ? "预选范围" : "当前项目";
                if (!ShowDetails(app, "DWG 转视图预览",
                    $"扫描范围：{scope}。转换成功后会替换旧链接，可用 Ctrl+Z 撤销。", plans, checkedPlans))
                    return Result.Cancelled;

                int converted = 0;
                int created = 0;
                var failures = new StringBuilder();
                using (var group = new TransactionGroup(doc, "DWG 转视图专用链接"))
                {
                    if (group.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法开始 DWG 转换。");

                    foreach (var plan in checkedPlans)
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
                    $"保留原链接 {plans.Count - converted} 个（跳过 {plans.Count(plan => !plan.CanConvert)} 个，" +
                    $"未勾选 {plans.Count(plan => plan.CanConvert) - checkedPlans.Count} 个，执行失败 {checkedPlans.Count - converted} 个）。";
                if (failures.Length > 0)
                    summary += "\n\n" + failures;
                ShowDetails(app, "DWG 转视图结果", summary);
                return converted > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("DWG 转视图", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static bool ShowDetails(UIApplication app, string title, string content,
            IReadOnlyList<DwgViewLinkMigration.Plan>? plans = null, List<DwgViewLinkMigration.Plan>? checkedPlans = null)
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

            if (plans != null && checkedPlans != null)
            {
                var hint = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
                DockPanel.SetDock(hint, Dock.Top);
                layout.Children.Add(hint);
                var rows = new StackPanel();
                var checks = new List<CheckBox>();
                var apply = new Button { Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
                void UpdateButton()
                {
                    apply.Content = $"转换勾选项（{checkedPlans.Count}）";
                    apply.IsEnabled = checkedPlans.Count > 0;
                }

                foreach (var plan in plans)
                {
                    string status = plan.CanConvert ? $"保留在 {plan.Views.Count} 个视图" : "保留原链接";
                    var details = new Expander
                    {
                        Header = status,
                        Margin = new Thickness(22, 0, 0, 8),
                        Content = new TextBlock
                        {
                            Text = string.Join("\n", plan.Blockers.Concat(plan.CanConvert ? plan.Views.Select(view => view.Name) : Enumerable.Empty<string>())),
                            TextWrapping = TextWrapping.Wrap
                        }
                    };
                    var check = new CheckBox
                    {
                        Content = new TextBlock { Text = plan.Name, TextWrapping = TextWrapping.Wrap },
                        IsChecked = plan.CanConvert,
                        IsEnabled = plan.CanConvert,
                        Margin = new Thickness(0, 4, 0, 2)
                    };
                    if (plan.CanConvert)
                    {
                        checkedPlans.Add(plan);
                        checks.Add(check);
                    }
                    check.Checked += (_, _) => { checkedPlans.Add(plan); UpdateButton(); };
                    check.Unchecked += (_, _) => { checkedPlans.Remove(plan); UpdateButton(); };
                    rows.Children.Add(check);
                    rows.Children.Add(details);
                }
                UpdateButton();
                if (checks.Count > 1)
                {
                    var selectAll = new Button { Content = "全选", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 6, 0) };
                    var clearAll = new Button { Content = "清空勾选", Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 12, 0) };
                    selectAll.Click += (_, _) => { foreach (var check in checks) check.IsChecked = true; };
                    clearAll.Click += (_, _) => { foreach (var check in checks) check.IsChecked = false; };
                    buttons.Children.Add(selectAll);
                    buttons.Children.Add(clearAll);
                }
                apply.Click += (_, _) => dialog.DialogResult = true;
                buttons.Children.Add(apply);
                layout.Children.Add(new ScrollViewer
                {
                    Content = rows,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }
            else
            {
                layout.Children.Add(new TextBox
                {
                    Text = content,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }
            buttons.Children.Add(new Button { Content = "关闭", IsCancel = true, Padding = new Thickness(12, 5, 12, 5) });
            return dialog.ShowDialog() == true;
        }
    }
}
