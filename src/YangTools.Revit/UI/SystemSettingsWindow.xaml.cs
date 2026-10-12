using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;
using YangTools.Revit.Mcp;
using Autodesk.Revit.UI;

namespace YangTools.Revit.UI
{
    public class CommandSettingItem : INotifyPropertyChanged
    {
        public string CommandFullName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool DefaultVisible { get; set; }
        public bool CanToggle { get; set; } = true;
        public string LockReason { get; set; } = "";

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                bool normalized = CanToggle ? value : true;
                if (_isVisible == normalized) return;
                _isVisible = normalized;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class ThemeOptionItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PreviewColor { get; set; } = "#808080";
        public string Description { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class SystemSettingsWindow : Window
    {
        private Dictionary<string, List<CommandSettingItem>> _panelSettings = new();
        private List<ThemeOptionItem> _themeOptions = new();
        private readonly DispatcherTimer _mcpRefreshTimer;
        private bool _loadingMcpSettings;

        public SystemSettingsWindow(int selectedTabIndex = 0)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            LoadThemeOptions();
            LoadPanelSettings();
            Assembly assembly = Assembly.GetExecutingAssembly();
            string displayVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "未知";
            VersionText.Text = displayVersion;
            SignatureText.Text = GetAuthenticodeSignerDisplay(assembly.Location);
            _mcpRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _mcpRefreshTimer.Tick += (_, _) => RefreshMcpStatus();
            LoadMcpSettings();
            if (selectedTabIndex >= 0 && selectedTabIndex < MainTabControl.Items.Count)
                MainTabControl.SelectedIndex = selectedTabIndex;
        }


        private static string GetAuthenticodeSignerDisplay(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                return "未检测到发布签名";

            try
            {
                X509Certificate certificate = X509Certificate.CreateFromSignedFile(assemblyPath);
                using (var certificate2 = new X509Certificate2(certificate))
                {
                    string subject = certificate2.GetNameInfo(X509NameType.SimpleName, false);
                    if (string.IsNullOrWhiteSpace(subject))
                        subject = certificate2.Subject;
                    return string.IsNullOrWhiteSpace(subject)
                        ? "已检测到 Authenticode 签名证书"
                        : "证书：" + subject;
                }
            }
            catch (CryptographicException)
            {
                return "未检测到 Authenticode 发布签名";
            }
            catch
            {
                return "发布签名状态无法读取";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMcpStatus();
            _mcpRefreshTimer.Start();
        }

        private void LoadMcpSettings()
        {
            _loadingMcpSettings = true;
            try
            {
                McpGatewaySettings settings = McpGatewaySettingsStore.Load();
                McpAutoStartCheck.IsChecked = settings.AutoStart;
                McpAllowPythonCheck.IsChecked = settings.AllowPythonExecution;
            }
            finally
            {
                _loadingMcpSettings = false;
            }
        }

        private void McpSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingMcpSettings) return;
            McpGatewaySettingsStore.Save(new McpGatewaySettings
            {
                AutoStart = McpAutoStartCheck.IsChecked == true,
                AllowPythonExecution = McpAllowPythonCheck.IsChecked == true
            });
        }

        private void RefreshMcpStatus()
        {
            RevitExecutionStatus execution = App.GlobalExecutionBroker?.GetStatus() ?? new RevitExecutionStatus();
            ExecutionStatusText.Text = execution.IsReady ? "就绪" : "未初始化";
            PythonStatusText.Text = execution.PythonAvailable
                ? "可用"
                : execution.PythonInitialized
                    ? "不可用：" + execution.PythonError
                    : "未初始化（首次执行时加载）";
            PendingCountText.Text = execution.PendingCount.ToString();
            CurrentCommandText.Text = string.IsNullOrWhiteSpace(execution.CurrentCommand) ? "无" : execution.CurrentCommand;
            ExecutionLastResultText.Text = !string.IsNullOrWhiteSpace(execution.LastError)
                ? "失败：" + execution.LastError
                : string.IsNullOrWhiteSpace(execution.LastResult) ? "无" : execution.LastResult;

            McpGatewayStatusSnapshot gateway = App.GlobalMcpGateway?.GetStatus() ?? new McpGatewayStatusSnapshot();
            McpStatusText.Text = gateway.IsRunning ? "MCP 网关正在监听" : "MCP 网关已关闭";
            McpUrlTextBox.Text = gateway.IsRunning ? gateway.ServerUrl : "未启动";
            McpTokenTextBox.Text = gateway.IsRunning ? gateway.Token : "未生成";
            McpInstanceFileTextBox.Text = gateway.IsRunning ? gateway.InstanceFilePath : "未生成";
            McpLastRequestText.Text = gateway.LastRequestAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "无";
            McpLastCommandText.Text = string.IsNullOrWhiteSpace(gateway.LastCommand) ? "无" : gateway.LastCommand;
            McpLastDurationText.Text = gateway.LastDurationMilliseconds > 0 ? gateway.LastDurationMilliseconds + " ms" : "-";
            McpLastErrorText.Text = string.IsNullOrWhiteSpace(gateway.LastError) ? "无" : gateway.LastError;
            McpStatusIndicator.SetResourceReference(
                System.Windows.Shapes.Shape.FillProperty,
                gateway.IsRunning ? "SuccessColor" : "DisabledText");
        }

        private void StartMcp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.GlobalMcpGateway?.Start();
                RefreshMcpStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MCP 启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopMcp_Click(object sender, RoutedEventArgs e)
        {
            App.GlobalMcpGateway?.Stop();
            RefreshMcpStatus();
        }

        private void RestartMcp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.GlobalMcpGateway?.Restart();
                RefreshMcpStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MCP 重启失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyMcpConfig_Click(object sender, RoutedEventArgs e)
        {
            McpGatewayStatusSnapshot gateway = App.GlobalMcpGateway?.GetStatus() ?? new McpGatewayStatusSnapshot();
            if (!gateway.IsRunning)
            {
                MessageBox.Show("请先启动 MCP 网关。", "MCP", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var config = new JObject
            {
                ["url"] = gateway.ServerUrl,
                ["token"] = gateway.Token,
                ["instanceFile"] = gateway.InstanceFilePath
            };
            Clipboard.SetText(config.ToString(Formatting.Indented));
            MessageBox.Show("连接配置已复制。", "MCP", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void TestMcp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string result = await (App.GlobalMcpGateway?.SelfTestAsync(CancellationToken.None)
                                      ?? Task.FromResult("MCP 网关未初始化。"));
                MessageBox.Show("MCP 自检通过。\n\n" + result, "MCP 自检", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MCP 自检失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===== 主题选项 =====
        private void LoadThemeOptions()
        {
            _themeOptions = ThemeManager.Presets.Select(t => new ThemeOptionItem
            {
                Id = t.Id,
                DisplayName = t.DisplayName,
                PreviewColor = t.PreviewColor,
                Description = t.Description,
                IsSelected = t.Id == ThemeManager.CurrentTheme.Id
            }).ToList();
            ThemeList.ItemsSource = _themeOptions;
        }

        private void Theme_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ThemeOptionItem option)
            {
                foreach (var t in _themeOptions) t.IsSelected = false;
                option.IsSelected = true;
                ThemeManager.SwitchTheme(option.Id);
            }
        }

        // ===== Ribbon 面板设置 =====
        private void LoadPanelSettings()
        {
            _panelSettings.Clear();
            PanelTabControl.Items.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            var commands = assembly.GetTypes()
                .Where(t => typeof(IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new { Type = t, Attr = t.GetCustomAttribute<RibbonButtonAttribute>() })
                .Where(x => x.Attr != null)
                .OrderBy(x => x.Attr!.PanelName, StringComparer.CurrentCulture)
                .ThenBy(x => x.Attr!.ButtonText, StringComparer.CurrentCulture)
                .ToList();

            foreach (var group in commands.GroupBy(x => x.Attr!.PanelName))
            {
                string panelName = group.Key;
                var list = new List<CommandSettingItem>();
                foreach (var cmd in group)
                {
                    bool defaultVisible = !cmd.Attr!.IsSlideOut;
                    bool isSystemSettings = cmd.Type == typeof(YangTools.Revit.Commands.SampleWindowCommand);
                    bool isVisible = isSystemSettings || RibbonConfigManager.IsCommandVisibleOnMainPanel(
                        panelName, cmd.Type.FullName!, defaultVisible);

                    list.Add(new CommandSettingItem
                    {
                        CommandFullName = cmd.Type.FullName!,
                        DisplayName = isSystemSettings ? cmd.Attr.ButtonText + "（固定）" : cmd.Attr.ButtonText,
                        DefaultVisible = isSystemSettings || defaultVisible,
                        CanToggle = !isSystemSettings,
                        LockReason = isSystemSettings ? "系统设置固定显示，用于恢复其他面板配置。" : "",
                        IsVisible = isVisible
                    });
                }

                _panelSettings[panelName] = list;
                var tabItem = new TabItem { Header = panelName };
                var listBox = new ListBox
                {
                    ItemsSource = list,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.Transparent
                };

                var template = new DataTemplate();
                var checkBox = new FrameworkElementFactory(typeof(CheckBox));
                checkBox.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(CommandSettingItem.IsVisible))
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay
                });
                checkBox.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding(nameof(CommandSettingItem.DisplayName)));
                checkBox.SetBinding(CheckBox.IsEnabledProperty, new System.Windows.Data.Binding(nameof(CommandSettingItem.CanToggle)));
                checkBox.SetBinding(FrameworkElement.ToolTipProperty, new System.Windows.Data.Binding(nameof(CommandSettingItem.LockReason)));
                checkBox.SetValue(CheckBox.MarginProperty, new Thickness(5));
                checkBox.SetValue(CheckBox.ForegroundProperty,
                    TryFindResource("PrimaryText") ?? System.Windows.Media.Brushes.Black);
                template.VisualTree = checkBox;
                listBox.ItemTemplate = template;
                tabItem.Content = listBox;
                PanelTabControl.Items.Add(tabItem);
            }

            if (PanelTabControl.Items.Count > 0)
                PanelTabControl.SelectedIndex = 0;
        }

        protected override void OnClosed(EventArgs e)
        {
            _mcpRefreshTimer.Stop();
            base.OnClosed(e);
        }

        private bool ValidatePanelSettings(out string error)
        {
            foreach (var panel in _panelSettings)
            {
                if (!panel.Value.Any(item => item.IsVisible))
                {
                    error = $"面板“{panel.Key}”至少需要保留一个主区按钮，否则其展开区可能无法访问。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void SavePanelSettings()
        {
            var snapshot = _panelSettings.ToDictionary(
                panel => panel.Key,
                panel => panel.Value.ToDictionary(item => item.CommandFullName, item => item.IsVisible));
            RibbonConfigManager.ReplacePanelSettings(snapshot);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSavePanel_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidatePanelSettings(out string error))
            {
                MessageBox.Show(error, "面板设置", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SavePanelSettings();
                MessageBox.Show("面板设置已保存。请重启 Revit 后生效。", "YangTools", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存面板设置失败：" + ex.Message, "YangTools", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private IEnumerable<CommandSettingItem> GetCurrentPanelItems()
        {
            if (PanelTabControl.SelectedItem is TabItem tab && tab.Header is string panelName &&
                _panelSettings.TryGetValue(panelName, out var items))
                return items;
            return Enumerable.Empty<CommandSettingItem>();
        }

        private void SelectCurrentPanel_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in GetCurrentPanelItems().Where(x => x.CanToggle))
                item.IsVisible = true;
        }

        private void RestoreCurrentPanel_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in GetCurrentPanelItems())
                item.IsVisible = item.DefaultVisible;
        }

        private void RestoreAllPanels_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _panelSettings.Values.SelectMany(x => x))
                item.IsVisible = item.DefaultVisible;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
    }
}
