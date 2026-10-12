# YangTools 临时插件箱开发指南 V3.1

> 适用于 YangTools 3.1.35 及以后版本。文件名继续保留 `MicroTool_Developer_Guide V3.md`，避免旧交接文档中的链接失效。

## 1. 功能定位

“临时插件箱”用于运行只服务于某个 Revit 项目的短期 DLL 工具。它适合快速开发、项目内验证和临时交付，不等于正式合入 YangTools。

- **临时项目仓库**：存放当前项目的短期工具。
- **收藏 / 候选转正仓库**：复制主 DLL、运行依赖和说明文件，供后续源码审查。
- **正式转正**：必须有源码，并完成版本、事务、UI、异常和实机验收；不能只凭一个 DLL 直接认定为正式功能。

## 2. 每个临时插件必须有说明

每个主 DLL 必须在同目录提供说明文件，优先命名：

```text
MyTool.dll
MyTool.microtool.json
```

也兼容目录级：

```text
microtool.json
```

收藏包中的 `microtool.package.json` 也会被读取。读取优先级为：

```text
<DLL名>.microtool.json
→ microtool.json
→ microtool.package.json
```

缺少说明时，临时插件箱会显示“缺少说明”。选择 DLL 后可点击 **创建说明模板**，程序会生成标准 JSON 并用记事本打开。

### 2.1 必填内容

至少必须填写：

- `DisplayName`：界面显示名称；
- `FunctionDescription`：做什么、处理什么对象、最终结果是什么；
- `UsageInstructions`：用户实际操作步骤。

强烈建议同时填写：

- `SupportedRevitVersions`；
- `RequiresSelection`；
- `WritesModel`；
- `UndoDescription`；
- `EntryCommand`；
- `Notes`。

### 2.2 标准示例

```json
{
  "SchemaVersion": 1,
  "DisplayName": "沿模型线放置柱",
  "FunctionDescription": "沿用户选择的连续模型线，按指定间距放置所选柱类型，并按路线方向旋转。",
  "UsageInstructions": [
    "打开需要放置柱的平面视图。",
    "选择连续模型线。",
    "选择柱类型并输入间距。",
    "确认标高、偏移和末端放置规则。",
    "点击运行并检查生成结果。"
  ],
  "SupportedRevitVersions": "2024",
  "EntryCommand": "YangTools.Temporary.Sample.ColumnAlongPathCommand",
  "RequiresSelection": true,
  "WritesModel": true,
  "UndoDescription": "生成结果合并为一个 Revit 撤销项。",
  "Notes": "模型线应位于同一平面；不支持跨文档选择。"
}
```

说明不得只写“测试工具”“临时功能”等无实际信息内容。

## 3. 推荐目录

```text
某临时项目/
└─ 某功能/
   ├─ MyTool.dll
   ├─ MyTool.microtool.json
   ├─ MyTool.pdb                 # 可选
   ├─ MyDependency.dll           # 自定义依赖
   ├─ runtimes/                  # 可选原生依赖
   └─ config.json                # 可选配置
```

临时插件箱会扫描 DLL。不要把 `RevitAPI.dll`、`RevitAPIUI.dll` 或大量无关第三方 DLL 当成主入口。

## 4. 实际加载方式

YangTools 使用影子复制加载：

```text
源插件目录
→ %LOCALAPPDATA%\YangTools\MicroToolCache\<Revit进程ID>\<本次运行GUID>
→ 从影子目录加载主 DLL 和依赖
```

因此源 DLL 通常可在不关闭 Revit 的情况下重新编译覆盖。但 .NET 程序集在当前 Revit 进程中不能真正卸载；同一会话反复加载不同版本可能增加内存占用。完成集中调试后应重启 Revit。

## 5. Revit 版本与目标框架

| Revit | 临时插件目标框架 |
|---|---|
| 2021–2024 | `net48` |
| 2025–2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

每个年份必须引用该年份的 `RevitAPI.dll` 和 `RevitAPIUI.dll`，并独立编译。一个针对 Revit 2024 编译的 DLL，不应在 2021、2025 或 2027 中直接混用。

```xml
<Reference Include="RevitAPI"
           HintPath="C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll"
           Private="False" />
<Reference Include="RevitAPIUI"
           HintPath="C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll"
           Private="False" />
```

自定义依赖应复制到主 DLL 同目录；Revit 自带程序集不要复制。

## 6. 命令入口规范

推荐一 DLL 一功能：一个公开、非抽象、无参数构造函数的 `IExternalCommand`。

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Temporary.Sample
{
    [Transaction(TransactionMode.Manual)]
    public sealed class RunCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
            {
                message = "请先打开项目。";
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}
```

一个 DLL 包含多个命令时，宿主会要求用户选择入口。说明文件中的 `EntryCommand` 应写完整类型名，便于识别。

## 7. 选择、UI 与 Revit 主线程

- 选择对象使用 `UIDocument.Selection`；
- WPF 窗口 Owner 使用 `commandData.Application.MainWindowHandle`；
- 不创建第二个 `System.Windows.Application`；
- 不从 `Task.Run()`、后台线程或普通计时器直接访问 Revit API；
- 非模态窗口修改模型时使用 `ExternalEvent`；
- 临时插件箱本身不会替工具自动创建事务。

说明文件必须写清：

- 是否需要预选；
- 先选什么、后选什么；
- 是否只支持当前视图；
- 是否修改模型；
- 如何撤销。

## 8. 事务与撤销

任何写操作由临时工具自己管理 `Transaction` 或 `TransactionGroup`。

```csharp
using (Transaction transaction = new Transaction(document, "临时插件：修改模型"))
{
    transaction.Start();
    try
    {
        // 修改模型
        TransactionStatus status = transaction.Commit();
        if (status != TransactionStatus.Committed)
            throw new InvalidOperationException($"事务提交失败：{status}");
    }
    catch
    {
        if (transaction.GetStatus() == TransactionStatus.Started)
            transaction.RollBack();
        throw;
    }
}
```

多阶段操作需要合并为一次 Ctrl+Z 时，使用 `TransactionGroup.Assimilate()`。批量操作应避免每个元素单独开启顶层事务。

## 9. 性能规范

- 优先一次 `FilteredElementCollector`，不要“每个目标参数 × 全项目扫描”；
- 大批量写入使用一个事务或合理分批；
- 不把几万条结果直接渲染进 WPF/Watch；详细结果写 CSV/JSON；
- 长任务输出阶段和计数；
- 不要为了 UI 响应，把 Revit API 调用移到后台线程；
- 使用内部参数值：`AsDouble/AsInteger/AsString/AsElementId`，不要用 `AsValueString()` 做数据回填。

## 10. 异常与返回值

- 用户取消：`Result.Cancelled`；
- 成功：`Result.Succeeded`；
- 失败：设置 `message` 并返回 `Result.Failed`，或抛出异常交给宿主记录；
- 不得吞掉导致部分修改、数据丢失或结果错误的异常。

宿主外层异常日志位于：

```text
%LOCALAPPDATA%\YangTools\Logs\
```

## 11. 发布前自检

每个临时插件至少完成：

1. 说明文件能被临时插件箱读取；
2. 当前 Revit 年份真实编译通过；
3. DLL 和依赖可从影子目录加载；
4. 入口可执行，取消选择不会报错；
5. 写操作产生明确撤销项；
6. 小模型功能结果正确；
7. 大模型性能可接受；
8. 锁定、成组、只读或缺少前置条件时有明确提示；
9. 关闭窗口后不残留线程、Hook 或第二个 WPF Application；
10. 说明中的步骤、适用版本和模型影响与实际一致。

## 12. 收藏与正式转正

- **收藏**：复制运行包和说明，仍是候选工具；
- **转正**：必须取得源码并审查，合入 YangTools 或制作独立 `.addin`；
- 没有源码、没有说明、没有实机测试的 DLL 不应标记为正式功能。
