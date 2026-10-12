using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YangTools.Revit.Commands;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class MicroToolWindow : Window
    {
        private readonly UserSettings _settings;
        private IReadOnlyList<MicroProjectItem> _projects = Array.Empty<MicroProjectItem>();
        private IReadOnlyList<MicroToolItem> _tools = Array.Empty<MicroToolItem>();

        public string? SelectedDllPath { get; private set; }

        public MicroToolWindow()
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _settings = UserSettings.Load();
            LoadProjects();
        }

        private void LoadProjects()
        {
            try
            {
                _projects = MicroToolEngine.GetProjects();
                ProjectsListBox.ItemsSource = _projects;

                MicroProjectItem? preferred = _projects.FirstOrDefault(project =>
                    string.Equals(project.FullPath, _settings.MicroToolLastProjectPath, StringComparison.OrdinalIgnoreCase));
                ProjectsListBox.SelectedItem = preferred ?? _projects.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ShowError("读取临时插件项目失败", ex);
            }
        }

        private void RefreshTools()
        {
            if (ProjectsListBox.SelectedItem is not MicroProjectItem selectedProject)
            {
                ProjectPathText.Text = "请选择临时项目或收藏仓库";
                ToolsListBox.ItemsSource = null;
                ToolCountText.Text = string.Empty;
                BtnRemoveProject.IsEnabled = false;
                BtnRun.IsEnabled = false;
                BtnCollect.IsEnabled = false;
                UpdateToolDocumentation(null);
                return;
            }

            ProjectPathText.Text = $"{selectedProject.RoleText}\n{selectedProject.FullPath}";
            BtnRemoveProject.IsEnabled = !selectedProject.IsDefault;
            BtnCollect.IsEnabled = false;
            _settings.MicroToolLastProjectPath = selectedProject.FullPath;
            _settings.Save();

            if (!selectedProject.Exists || !Directory.Exists(selectedProject.FullPath))
            {
                _tools = Array.Empty<MicroToolItem>();
                ToolsListBox.ItemsSource = _tools;
                ToolCountText.Text = "路径不存在";
                StatusText.Text = "该仓库路径不存在，可移除后重新添加。";
                BtnRun.IsEnabled = false;
                BtnCollect.IsEnabled = false;
                UpdateToolDocumentation(null);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _tools = MicroToolEngine.GetTools(selectedProject.FullPath);
                ToolsListBox.ItemsSource = _tools;
                ToolCountText.Text = $"{_tools.Count} 个可运行入口";
                StatusText.Text = _tools.Count == 0
                    ? "当前位置没有可运行的临时插件。"
                    : selectedProject.IsDefault
                        ? "这里是收藏/候选转正仓库；真正转正仍需源码审查。"
                        : "这是临时项目仓库，可运行、调试或收藏到默认仓库。";

                MicroToolItem? preferred = _tools.FirstOrDefault(tool =>
                    string.Equals(tool.FullPath, _settings.MicroToolLastToolPath, StringComparison.OrdinalIgnoreCase));
                ToolsListBox.SelectedItem = preferred;
                BtnRun.IsEnabled = ToolsListBox.SelectedItem != null;
                BtnCollect.IsEnabled = ToolsListBox.SelectedItem != null && !selectedProject.IsDefault;
                UpdateToolDocumentation(ToolsListBox.SelectedItem as MicroToolItem);
            }
            catch (Exception ex)
            {
                ShowError("扫描微工具 DLL 失败", ex);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ProjectsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTools();
        }

        private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasTool = ToolsListBox.SelectedItem is MicroToolItem;
            BtnRun.IsEnabled = hasTool;
            BtnCollect.IsEnabled = hasTool
                && ProjectsListBox.SelectedItem is MicroProjectItem project
                && !project.IsDefault;

            if (ToolsListBox.SelectedItem is MicroToolItem tool)
            {
                StatusText.Text = tool.HasDocumentation
                    ? tool.FullPath
                    : "该插件缺少完整的功能/使用说明；请先创建并填写说明模板。";
                _settings.MicroToolLastToolPath = tool.FullPath;
                _settings.Save();
                UpdateToolDocumentation(tool);
            }
            else
            {
                UpdateToolDocumentation(null);
            }
        }

        private void UpdateToolDocumentation(MicroToolItem? tool)
        {
            if (tool == null)
            {
                DocumentationStatusText.Text = "未选择插件";
                FunctionDescriptionText.Text = "选择一个 DLL 后显示功能说明。";
                UsageInstructionsText.Text = string.Empty;
                CompatibilityText.Text = string.Empty;
                return;
            }

            DocumentationStatusText.Text = tool.DocumentationBadge;
            FunctionDescriptionText.Text = tool.FunctionDescription;
            UsageInstructionsText.Text = tool.UsageSummary;
            CompatibilityText.Text = $"{tool.CompatibilityText} · {tool.ModelWriteText}";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private const string MicroToolBoxHelp =
            "功能定位\r\n" +
            "临时插件箱用于运行只服务于某个 Revit 项目的短期 DLL 工具。它不是正式插件商店，也不会自动把临时 DLL 注册到 Ribbon。\r\n\r\n" +
            "仓库\r\n" +
            "左侧普通仓库：项目开发或调试目录。\r\n" +
            "收藏/候选转正仓库：复制主 DLL、依赖与说明文件，供后续审查转正。\r\n\r\n" +
            "运行方式\r\n" +
            "1. 添加临时项目目录。\r\n" +
            "2. 选择带 IExternalCommand 入口的主 DLL。\r\n" +
            "3. 阅读“插件说明”，确认选择条件、模型影响和适用 Revit 版本。\r\n" +
            "4. 点击运行；如果一个 DLL 有多个命令，会继续选择入口。\r\n\r\n" +
            "说明文件\r\n" +
            "每个主 DLL 应提供 <DLL名>.microtool.json，至少包含功能说明和使用步骤。缺少时点击“创建说明模板”。\r\n\r\n" +
            "重要限制\r\n" +
            "临时 DLL 必须针对当前 Revit 年份独立编译。宿主不会自动开启事务，也不会保证不同年份 DLL 可以混用。\r\n" +
            "运行临时 DLL 等同在当前 Revit 进程执行其代码，没有沙箱；只运行自己编译或已核验来源的 DLL。\r\n" +
            "收藏包仅允许删除带有效清单的默认仓库直属目录；链接目录、无主 DLL 或不匹配的清单会被拒绝。";

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            FeatureHelpWindow.Show(this, "临时插件箱说明", MicroToolBoxHelp);
        }

        private void DeveloperGuideButton_Click(object sender, RoutedEventArgs e)
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(MicroToolWindow).Assembly.Location) ?? string.Empty;
            string guidePath = Path.Combine(assemblyDirectory, "Docs", "MicroTool_Developer_Guide V3.md");
            if (File.Exists(guidePath))
            {
                try
                {
                    Process.Start("notepad.exe", guidePath);
                    return;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("打开临时插件开发规范", ex);
                }
            }

            FeatureHelpWindow.Show(this, "临时插件开发规范",
                "开发规范文件：MicroTool_Developer_Guide V3.md（内容版本 V3.1）\r\n\r\n" +
                "核心要求：\r\n" +
                "1. 一 DLL 一功能；公开无参数构造的 IExternalCommand。\r\n" +
                "2. 每个主 DLL 必须附带 <DLL名>.microtool.json。\r\n" +
                "3. 写模型由工具自己管理 Transaction/TransactionGroup。\r\n" +
                "4. 每个 Revit 年份使用对应 API 和目标框架独立编译。\r\n" +
                "5. 功能说明必须写清前置选择、操作步骤、模型修改和撤销方式。");
        }

        private void ToolHelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem tool)
            {
                MessageBox.Show("请先选择一个临时插件。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            FeatureHelpWindow.Show(this, tool.DisplayName + " - 插件说明", tool.BuildFullDocumentation());
        }

        private void CreateDocumentationButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem tool)
            {
                MessageBox.Show("请先选择一个临时插件。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CreateDocumentationForTool(tool);
        }

        private void CreateDocumentationForTool(MicroToolItem tool)
        {
            try
            {
                bool overwrite = false;
                if (!string.IsNullOrWhiteSpace(tool.DocumentationPath) && File.Exists(tool.DocumentationPath))
                {
                    MessageBoxResult result = MessageBox.Show(
                        "该插件已有说明文件。是否用标准模板覆盖？\n\n原说明内容会被替换。",
                        "覆盖说明模板",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                        return;
                    overwrite = true;
                }

                string path = MicroToolEngine.CreateDocumentationTemplate(tool.FullPath, overwrite);
                Process.Start("notepad.exe", path);
                StatusText.Text = "已创建说明模板；填写功能说明和使用步骤，保存后点击刷新。";
                RefreshTools();
            }
            catch (Exception ex)
            {
                ShowError("创建临时插件说明模板失败", ex);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSelectedTool();
        }

        private void ToolsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedTool();
        }

        private void ExecuteSelectedTool()
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem selectedTool)
            {
                MessageBox.Show("请先选择一个临时插件 DLL。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(selectedTool.FullPath))
            {
                MessageBox.Show("所选 DLL 已被移动或删除，请刷新列表。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshTools();
                return;
            }

            if (!selectedTool.HasDocumentation)
            {
                MessageBoxResult create = MessageBox.Show(
                    "该临时插件缺少完整的功能说明或使用步骤。\n\n" +
                    "临时插件箱要求每个工具先补充说明，避免以后不知道怎么使用或误操作模型。\n\n" +
                    "是否现在创建说明模板？",
                    "缺少插件说明",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (create == MessageBoxResult.Yes)
                    CreateDocumentationForTool(selectedTool);
                return;
            }

            SelectedDllPath = selectedTool.FullPath;
            _settings.MicroToolLastToolPath = selectedTool.FullPath;
            _settings.Save();
            DialogResult = true;
            Close();
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewProjectDialog { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                MicroToolEngine.AddProjectConfig(dialog.ProjectName, dialog.FolderPath);
                _settings.MicroToolLastProjectPath = Path.GetFullPath(dialog.FolderPath);
                _settings.Save();
                LoadProjects();
            }
            catch (Exception ex)
            {
                ShowError("添加临时项目失败", ex);
            }
        }

        private void DeleteProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectsListBox.SelectedItem is not MicroProjectItem selectedProject)
            {
                MessageBox.Show("请先选择要移除的临时项目。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selectedProject.IsDefault)
            {
                MessageBox.Show("收藏/候选转正仓库不能移除。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"确定从列表中移除仓库“{selectedProject.Name}”吗？\n\n不会删除磁盘上的任何文件。",
                "移除仓库",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            string removedPath = selectedProject.FullPath;
            MicroToolEngine.RemoveProjectConfig(removedPath);
            _settings.MicroToolLastProjectPath = string.Empty;
            _settings.MicroToolLastToolPath = string.Empty;
            _settings.Save();
            LoadProjects();
            if (Directory.Exists(removedPath))
                OpenFolder(removedPath);
        }

        private void OpenProjectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectsListBox.SelectedItem is MicroProjectItem project)
                OpenFolder(project.FullPath);
        }

        private void OpenToolFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem tool)
                return;

            try
            {
                Process.Start("explorer.exe", $"/select,\"{tool.FullPath}\"");
            }
            catch (Exception ex)
            {
                ShowError("打开 DLL 位置失败", ex);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshTools();
        }

        private void CollectToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem selectedTool)
                return;
            if (ProjectsListBox.SelectedItem is not MicroProjectItem project || project.IsDefault)
            {
                MessageBox.Show("请选择临时项目中的插件。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!selectedTool.HasDocumentation)
            {
                MessageBox.Show("收藏前必须填写完整的功能说明和使用步骤。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                CreateDocumentationForTool(selectedTool);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                $"将“{selectedTool.Name}”及其运行依赖复制到收藏/候选转正仓库。\n\n" +
                "这只是收藏，不会自动变成 Ribbon 独立插件；真正转正仍需源码审查和集成。",
                "收藏临时插件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                bool overwrite = false;
                try
                {
                    string collected = MicroToolEngine.CollectToolPackage(selectedTool.FullPath, false);
                    MessageBox.Show($"已收藏到：\n{collected}", "临时插件箱",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (MicroToolPackageConflictException)
                {
                    MessageBoxResult replace = MessageBox.Show(
                        "收藏仓库中已有同名插件包，是否覆盖更新？",
                        "更新收藏插件",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (replace != MessageBoxResult.Yes)
                        return;
                    overwrite = true;
                    string collected = MicroToolEngine.CollectToolPackage(selectedTool.FullPath, overwrite);
                    MessageBox.Show($"已更新收藏：\n{collected}", "临时插件箱",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError("收藏临时插件失败", ex);
            }
        }

        private void CleanupCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int removed = MicroToolEngine.CleanupOldRunCaches(TimeSpan.Zero);
                StatusText.Text = removed == 0
                    ? "没有可清理的旧运行缓存；当前 Revit 会话使用中的缓存需关闭 Revit 后清理。"
                    : $"已清理 {removed} 个旧 Revit 会话缓存目录。";
            }
            catch (Exception ex)
            {
                ShowError("清理临时插件运行缓存失败", ex);
            }
        }

        private void DeletePluginButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is not MicroToolItem selectedTool)
            {
                MessageBox.Show("请先选择要删除的 DLL。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string deleteDescription = selectedTool.IsCollectedPackage
                ? $"确定删除收藏包“{selectedTool.Name}”吗？\n\n将删除该收藏包目录中的主 DLL、依赖和清单。此操作无法撤销。"
                : $"确定删除“{selectedTool.Name}”及其同名调试/配置旁车文件吗？\n\n不会删除其他共享依赖 DLL，此操作无法撤销。";
            MessageBoxResult result = MessageBox.Show(
                deleteDescription,
                selectedTool.IsCollectedPackage ? "删除收藏包" : "删除临时插件文件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (selectedTool.IsCollectedPackage)
                    MicroToolEngine.DeleteCollectedPackage(selectedTool.FullPath);
                else
                    DeleteToolSidecars(selectedTool.FullPath);
                _settings.MicroToolLastToolPath = string.Empty;
                _settings.Save();
                RefreshTools();
            }
            catch (Exception ex)
            {
                ShowError("删除微工具文件失败", ex);
            }
        }

        private static void DeleteToolSidecars(string dllPath)
        {
            string? directory = Path.GetDirectoryName(dllPath);
            string baseName = Path.GetFileNameWithoutExtension(dllPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("无法确定 DLL 所在目录。 ");

            var paths = new[]
            {
                dllPath,
                Path.Combine(directory, baseName + ".pdb"),
                Path.Combine(directory, baseName + ".xml"),
                Path.Combine(directory, baseName + ".config"),
                dllPath + ".config",
                Path.Combine(directory, baseName + ".deps.json"),
                Path.Combine(directory, baseName + ".runtimeconfig.json"),
                Path.Combine(directory, baseName + ".runtimeconfig.dev.json")
            };

            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show("仓库路径不存在。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start("explorer.exe", path);
        }

        private static void ShowError(string operation, Exception ex)
        {
            string logPath = DiagnosticLog.WriteError(operation, ex);
            MessageBox.Show(
                $"{operation}：\n{ex.Message}" +
                (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n\n诊断日志：{logPath}"),
                "临时插件箱",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
