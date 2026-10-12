# YangTools 3.1.35 — Loft / 实体提取目标标高修复报告

日期：2026-07-30  
范围：仅修复实体生成（Loft）与实体提取的目标标高/世界高度问题；不新增功能。

## 1. 用户实机问题

用户确认：实体生成（Loft）和实体提取选择目标标高后，生成模型的实际高度与原几何不一致，表现为目标标高高度没有正确加减或被重复计算。

## 2. 根因

旧实现采用以下流程：

1. 把几何转换到族局部坐标；
2. 在目标标高的 `Elevation` 上创建实例；
3. 从多个 BuiltInParameter 中猜测一个“偏移”参数；
4. 写入 `原锚点 Z - 目标标高 Elevation`，试图恢复原世界高度。

风险在第 3 步。不同族模板、族类别和 Revit 版本实际控制实例高度的参数可能不同。只取第一个存在且可写的参数，不能证明它就是当前实例的有效定位参数，因此可能出现：

- 目标标高被重复计入；
- 偏移写入无效参数；
- 实例关联标高正确，但世界 Z 错误；
- Loft 与实体提取因锚点不同而出现不同偏移结果。

## 3. 修复方案

新增 `Core/FamilyPlacementHelper.cs`，统一两个生成器的放置规则：

1. 目标标高只作为实例的标高关联；
2. `NewFamilyInstance` 的位置直接传入原几何的世界坐标锚点；
3. Revit 再生后读取实例真实 `LocationPoint.Point`；
4. 若 Revit/族模板重新解释了位置，则使用 `ElementTransformUtils.MoveElement` 做一次确定性修正；
5. 再次再生并验证最终世界坐标，误差超过 `1e-6 ft` 时抛错并回滚；
6. 验证实例 `LevelId` 必须等于用户选择的目标标高；
7. 日志记录目标标高、期望坐标、初始误差和最终误差。

### Loft 锚点

- X/Y：第一个轮廓的首点；
- Z：所有轮廓的最低 Z；
- 族几何以该锚点为局部原点；
- 放置后族局部原点必须回到同一个项目世界坐标。

### 实体提取锚点

- 使用全部提取 Solid 的联合包围框中心；
- 族几何以该中心为局部原点；
- 放置后族局部原点必须回到源实体联合中心。

## 4. 同步修复的确定性编译风险

根据 `final.7z` 的过程摘要，并对源码逐项复核，修复了能从源码直接确认的问题：

- `DistributeTextWindow.xaml.cs`：WPF `ComboBox` 与 `Autodesk.Revit.UI.ComboBox` 命名冲突；
- `ChineseCheckWindow.xaml.cs`：WPF `Grid` 与 `Autodesk.Revit.DB.Grid` 命名冲突；
- `AssistantMarkdownFormatter.cs`：显式使用 `System.Windows.Media.FontFamily`；
- `FeatureHelpWindow.xaml`：移除 .NET Framework 下不稳定的 `TextBox.LineHeight`；
- `ParameterDeleteService.cs`：局部变量 `record` 改为 `rec`；
- `ParameterApiCompatibility.cs`：Revit 2021 数据类型读取改为反射，避免直接依赖旧 `Definition.ParameterType`；
- `Verify-SourceBaseline.ps1`：修正 Markdown 格式器的错误静态令牌，并增加两个生成器必须使用共享世界坐标放置助手的门禁。

`final.7z` 没有包含修改源码、原始构建日志、DLL、binlog、Journal 或截图，因此其中其余“已修复/已通过”声明仍不能直接升级为验收结论。

## 5. 已完成的本环境检查

- UI/XAML/资源/编码静态检查：通过；
- 重复参数清理纯逻辑链路：通过；
- 目标标高放置契约：通过；
- Loft 与实体提取均不再调用 `SetInstanceOffset`；
- 16 组“源世界 Z × 目标标高 Z”纯数学回归：通过；
- Python 工具语法：通过。

## 6. 必须完成的 Windows/Revit 验收

静态检查不能证明 Revit 实际放置结果。必须在 Revit 2024 至少完成：

1. Loft：选择不同高度的两个以上轮廓，分别选择 0 标高、正高度标高、负高度标高；
2. 实体提取：源图元带非零标高和偏移，分别选择不同目标标高；
3. 对比源/生成几何的 MinZ、MaxZ、中心 Z 和实例 LocationPoint；
4. 确认 `LevelId` 等于所选标高；
5. 世界坐标误差不超过 1 mm；
6. `Ctrl+Z` 一次撤销完整恢复；
7. 收集 YangTools 日志、Journal、截图和坐标表。

完整执行步骤见 `docs/DEEPSEEK_CODEX_GENERATOR_LEVEL_ACCEPTANCE_3.1.35_20260730.md`。

## 7. 当前判断

- 源码修复：已完成；
- 静态审查：GO；
- Windows 编译：待执行；
- Revit 几何位置回归：待执行；
- 发布：NO-GO；
- 综合：条件 GO，可进入 Windows 编译与专项实机复验。
