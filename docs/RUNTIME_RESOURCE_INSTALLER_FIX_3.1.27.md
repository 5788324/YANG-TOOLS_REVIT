# YangTools 3.1.27 运行时资源与安装器修复报告

## 1. 用户反馈对应关系

### “DeepSeek 编译部署成功，但安装器说缺少 Revit 2021”

两件事并不矛盾：

- `dotnet build` 生成 `src\YangTools.Revit\bin\x64\2024`。
- `DeployAfterBuild=true` 直接复制到 `%APPDATA%\Autodesk\Revit\Addins\2024`。
- 3.1.26 安装器只读取自己的 `payload\年份`，不知道本地 bin 已存在。
- 当用户同时勾选 2021 时，安装器先检查 2021 并整体终止。

3.1.27 改为按年份独立处理：能找到真实构建产物的年份继续安装，缺失年份只跳过并记录。

## 2. StaticResourceExtension 根因

资源键存在不代表运行时一定可解析。YangTools 在 Revit 宿主进程中运行，没有独立 WPF Application。ThemeManager 在 `InitializeComponent` 前把主题和共享资源挂到根元素，但部分 XAML 同时声明自己的 `Window.Resources` 或 `UserControl.Resources`。

在这些局部资源被解析时，类似下面的外部静态继承存在时序风险：

```xml
<Style TargetType="DataGrid"
       BasedOn="{StaticResource SharedDataGridStyle}" />
```

WPF 需要在创建该 Style 的当下立即找到 `SharedDataGridStyle`。局部资源字典尚在构造时，Revit 宿主下的外部字典可能暂时不在查找链中，于是抛出 `StaticResourceExtension`。

## 3. 修复原则

- 同一 XAML 文件内定义并引用的局部资源：保留 `StaticResource`。
- ThemeManager 后附着的主题/共享资源：使用 `DynamicResource`。
- `Style.BasedOn` 不是依赖属性，不能使用 `DynamicResource`：相关局部样式改为自包含。
- ThemeManager 不写 `Application.Current.Resources`，继续避免污染 Revit/ACC 全局 WPF 资源。

## 4. 覆盖面板

直接发现外部静态继承的高风险面板：

1. `AssistantModeView`
2. `BatchTaskWindow`
3. `ChineseCheckWindow`
4. `FamilyManagerWindow`
5. `FamilyInstanceManagerWindow`
6. `SheetManagerWindow`

同时扫描全部业务 XAML，外部共享 `StaticResource` 残留为 0。

## 5. 安装器来源优先级

1. 源码目录存在时优先使用本地编译结果 `src\YangTools.Revit\bin\x64\年份`。
2. 正式安装包使用 `installer\payload\年份`。
3. 最后回退到 `artifacts\payload\年份`。

每个来源必须同时包含：

- `YangTools.Revit.dll`
- `YangTools.Revit.addin`
- `UI\SharedStyles.xaml`
- `UI\Themes\LightTheme.xaml`
- `DarkTheme.xaml`
- `ProfBlueTheme.xaml`
- `AppleTheme.xaml`

缺少任一关键文件，该年份不会被认定为可安装。

## 6. 验证边界

本轮已完成静态资源作用域检查和脚本结构检查，但当前环境不能运行 Revit。必须由 Windows/Revit 2024 重新编译部署后执行高风险面板烟雾测试。
