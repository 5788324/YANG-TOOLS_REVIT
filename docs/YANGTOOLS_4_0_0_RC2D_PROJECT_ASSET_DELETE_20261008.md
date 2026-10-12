# YangTools 4.0.0 RC2D｜项目资产删除 UI 与 Transaction 最小重构

日期：2026-10-08。基线为 RC2C，SHA-256：`893B69EEEEB3E9BCB8EB4FE54BF4F8A36C0CE8A59CCFC51EF67DA00E97D6ABBC`。

## 边界

只调整 `ProjectAssetManagerWindow` 的 `DeleteCheckedNodes_Click`、`DeleteSelected_Click`、`ExecuteDelete`；新增 `Core/ProjectAssets/ProjectAssetDeleteService.cs`，内含 `ProjectAssetDeleteRequest`、`ProjectAssetDeletePreview`、`ProjectAssetDeleteResult`。不调整 XAML、逐格编辑/Excel/批量重命名、其他 WPF 窗口、MCP 27 命令、Rebar/Ribbon、csproj、安装器。保留原中文确认和 Ctrl+Z 交互语义。

## 发现的风险与针对性修复

1. 旧删除将“Trial Delete → RollBack → TaskDialog → Real Delete → Assimilate”放进同一个 Revit TransactionGroup。新 Service 先单独预演并检查回滚结果，UI 提示连带 ID，确认后才进入正式单 Transaction（一次 Undo）。未确认不写模型。
2. 旧 UI 删除后只按用户选择 ID 清理三列表与当前节点；连带删除后其他行及树缓存可能显示已不存在图元。改为根据正式 `Document.Delete` 返回的所有 ID 清理三个列表和当前树的所有节点 Tag 列表。
3. 旧“读取勾选 ID”会在排队后才查询树，快速切换可能读到别的节点。点击时捕获 Tab/Tree/版本，再在 ExternalEvent 回调中核验；已过期则不执行删除。
4. 新逻辑将正式删除结果与预演的完整集合比较；不一致时在尚未提交的正式 Transaction 内抛错回滚，不默许新的关联图元被删除。
5. 删除已提交但界面刷新异常时不会报告“删除失败”，而是明确“删除已提交，界面刷新失败”，降低重复删除的风险。
6. Revit 自定义线样式可能使用负值 Category ID，不能凭 `GetElement` 返回 null 断言其不可删除；交由 Revit 的试删除判断，非法 ElementId 拒绝。

## 约束及仍存在的边界

- 以上是源码层合同与 XAML XML 检查，**不是 Windows .NET 编译、Revit 加载、Delete/Undo/GUI 运行测试**。Revit 返回 pending/失败状态、类别 ID 的真实行为和 ExternalEvent 调度仍待未来实机证据；Release 不可 GO。
- 删除线样式中内置或不能删除的类别由 Revit API 在预演中抛错，用户会看到失败原因；本轮不将其模拟成已删除。
- 删除已提交后 UI 可能在窗口关闭/切换时跳过当前视图刷新；重新进入该节点读取数据即可核对模型状态。
- 逐格参数编辑中原有的捕获异常/还原文本与事务失败问题不属于本轮修复，按 RC2E 单独处理。

## 验证

- 原项目全部 `Test-*.py` 静态套件（包括 RC2A、RC2B、RC2C）及新增 RC2D；需新包二次解压复测。
- RC2D 专项检查 typed request/preview/result、回滚/提交边界、级联验证、UI 状态、43 个 XAML XML 和按钮事件绑定。
- ZIP CRC、SHA-256、差异清单、中文安装器与生成物排除检查。

**状态**：SOURCE STATIC 候选；Windows/Revit Runtime NOT_TESTED；YangTools 4.0.0 RELEASE NO-GO。正式稳定版本仍为 YangTools 3.1.36 FINAL_E2E。
