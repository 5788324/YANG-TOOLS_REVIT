# DeepSeek 编译部署任务：YangTools 3.1.30

## 分工

- 不推送 Git，不创建 PR。
- 只在用户 Windows/Revit 2021 环境完成编译、部署和实机验收。
- 使用全新的 3.1.30 解压目录，禁止覆盖 3.1.29 或独立参数清理插件目录。
- 不重新设计参数清理算法；只修复真实编译错误或实机故障。

## 本轮目标

将独立的“重复项目参数清理”正式并入 YangTools：

```text
YangTools → 检查工具区 → 重复项目参数清理
```

确认新图标、四套主题、功能说明、扫描预览、快速处理、清理后自动复核和撤销均可用。

## 编译部署

1. 完全关闭全部 Revit 进程。
2. 在源码根目录执行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
& ".\tools\Verify-SourceBaseline.ps1"
& ".\tools\Build-All.ps1" -Versions 2021 -Deploy
```

`-Deploy` 会安装到当前用户目录，并自动删除本项目历史独立插件：

```text
%APPDATA%\Autodesk\Revit\Addins\2021\RevitParameterDeduplicator.addin
%APPDATA%\Autodesk\Revit\Addins\2021\RevitParameterDeduplicator\
```

不得保留两套入口。

## 编译验收

必须报告：

```text
编译：成功/失败
Warnings：
Errors：
YangTools.Revit.dll 路径和大小：
部署目录：
旧独立插件是否已移除：
```

若出现 API 版本错误，优先检查：

- Revit 2021：`ParameterType` / `BuiltInParameterGroup`。
- Revit 2022+：`ForgeTypeId` / `GetDataType()` / `GetGroupTypeId()`。
- 不得通过删除兼容层或只保留单一年份来规避错误。

## GUI 验收

启动 Revit 2021，打开用户提供的 RVT **副本**。

### Ribbon

确认：

```text
检查工具区 → 重复项目参数清理
```

- 只有一个入口。
- 图标为新的参数收敛/清理线条图标。
- 按钮名称完整。

### 窗口

- 窗口标题为“重复项目参数清理”。
- 跟随 YangTools 当前主题。
- 分别切换暖光米白、深色、专业蓝、Apple 主题后重新打开，不能出现白底、资源报错或文字不可见。
- “功能说明”位于“关闭”旁边，可正常打开。
- 运行输出文字可以拖选复制。

## 扫描验收

点击“扫描重复参数”，记录：

```text
扫描耗时：
项目参数总数：
唯一名称：
不重复参数：
重复参数总数：
重复组：
可处理组：
不兼容组：
```

项目历史基线约为：2750 个项目参数、606 个重复组、扫描约 55 ms。结果允许因模型状态变化而不同。

运行输出必须出现“扫描预览报告”，包含：

- 理论清理后项目参数数。
- 理论剩余重复组。
- 理论不重复参数数。
- 已知受保护来源数。
- 保守至少可删除数。
- 已知至少仍会剩余的重复组。
- 预计最多形成的不重复参数数。
- 执行后会重新扫描的说明。

## 快速处理验收

在 RVT 副本中点击：

```text
快速处理：合并并清理
```

记录：

```text
绑定处理耗时：
无需 ReInsert 组数：
实际 ReInsert 组数：
实例元素检查数：
类型元素检查数：
值复制数：
冲突数：
错误数：
删除成功数：
受保护/级联回滚数：
总耗时：
```

处理完成后程序必须自动重新扫描并输出：

```text
项目参数：清理前 → 清理后
重复组：清理前 → 清理后
不重复参数：清理前 → 清理后
清理后仍有重复参数：是/否
```

CSV 目录必须包含 `07_清理后复核.csv`。

## 数据原则回归检查

必须确认源码和运行行为仍满足：

1. 实例元素只遍历一次，类型元素只遍历一次。
2. 按 `ParameterElement.Id` 匹配，不使用 `LookupParameter(name)`。
3. 保留原参数，不采用全部删除重建。
4. Double/Integer/String/ElementId 按内部 `StorageType` 直接复制。
5. `AsValueString()` 只用于显示，不参与回填。
6. `Document.Delete()` 返回额外 ID 时只回滚当前来源参数。

## 撤销

快速处理完成后执行一次 Revit Undo，撤销项应为：

```text
重复项目参数：合并并清理
```

撤销后重新扫描，确认参数数量和重复组恢复。

## 最终交付

返回一份验收报告，附：

- 编译输出全文摘要。
- Ribbon 和窗口截图。
- 扫描预览文本。
- 快速处理完整日志。
- 清理后复核文本。
- CSV 报告目录。
- 撤销结果。
- 所有真实异常全文。
