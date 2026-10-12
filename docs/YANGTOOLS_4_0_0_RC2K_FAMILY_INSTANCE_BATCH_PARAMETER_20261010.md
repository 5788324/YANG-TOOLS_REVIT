# YangTools 4.0.0 RC2K｜族实例批量参数写入安全（2026-10-10）

以 RC2J 为唯一源码输入，仅重构 `FamilyInstanceManagerWindow.BatchSetParameter_Click` 对应的批量参数写入路径，不修改批量换型、Excel、删除或其他资产功能。

新增 `Core/FamilyInstances/FamilyInstanceBatchParameterService.cs` typed Request/Result/Service：全部目标预检（Document、ID 重复、参数身份、可写性、OnlyEmpty），未变更时不创建 Undo；单 Revit Transaction 进行全部写入，任一异常尝试回滚整批，回滚不确定明确提示而非报告成功；Commit 后重新读取参数，区分已经提交却无法确认回读的情况。原 UI 控件不变，WPF 捕获请求快照并对 Closed/Tab/筛选/行选择/重复请求检查，模型成功后 UI 刷新异常独立提示。原旧行为允许子事务单行失败后继续提交其他行，RC2K 改为整批原子提交（行为变更）。

`BatchChangeType_Click` 的逐行 `SubTransaction` 仍未修改，保留为下一批待办。`ImportExcel_Click`、删除与 Fill/Copy 仍在风险清单。未新增 MCP 命令，27 个旧 MCP 路由、Rebar、Ribbon、43 XAML、.csproj、打包规则不变。所有功能最终目标仍为 MCP 可调用，现阶段新的 Service 是可复用的执行层而非已经可通过 MCP 调用。

## 验收限制

静态验证包括新增 `tools/Test-FamilyInstanceRC2K.py`，原 `Test-FamilyManagementV2.py` 中原有“仅填空值”断言改到新 Service 检查（未删语义）；全量 `Test-*.py`、XAML/UI/Ribbon/MCP 回归在源码与 fresh ZIP 双环境运行；静态检查不代表 .NET 编译、Revit 加载或真正 Ctrl+Z。Windows C# Build、Revit Loaded/GUI/Write/Undo 一律 NOT_TESTED，4.0.0 RELEASE NO-GO；3.1.36 FINAL_E2E 仍正式稳定版。Mimo 停用、无需用户手工测试、不推 Git。
