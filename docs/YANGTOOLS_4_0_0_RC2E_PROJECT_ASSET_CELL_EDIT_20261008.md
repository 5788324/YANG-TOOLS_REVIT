# YangTools 4.0.0 RC2E｜项目资产逐格编辑 / UI 静态候选（2026-10-08）

## 范围

- 唯一输入 4.0.0 RC2D SHA `576F6F3054874971BF2732CEB6EBA78C367C1A9B07DD097080F9F53313E2C2BC`。
- `ProjectAssetManagerWindow.AssetDataGrid_CellEditEnding` 三条路径：资产名称、普通参数 `Parameters[...].Value`、Project Info 的 Value 列。
- 新建 `Core/ProjectAssets/ProjectAssetCellEditService.cs`，包含 typed Request/Result/EditKind，Service 无 WPF 依赖，Revit 主线程通过绑定原文档的 ExternalEvent 调用。
- 单个有效值修改 = 单个 Transaction；预检无效文档、只读文档、已删除图元、空名称、参数不存在或只读；没有改动时不创建事务或 Undo；写入失败显式回滚并报告不确定回滚状态。
- Project Info 行的 AssetId 实际是 Parameter.Id，因此使用 Parameter.Id 和参数名匹配原始参数，拒绝凭同名 LookupParameter 随意写入其他参数。
- WPF 三处可编辑列统一 `UpdateSourceTrigger.Explicit`：输入不会先将 VM 改为未提交的新值，也不使用 `e.Cancel=true` 让 DataGrid 卡在编辑状态；Service 成功后才更新当前有效 VM。
- 记录 Tab/Tree/版本/行引用，关闭或切换后排队写入被跳过，历史回调不污染新视图。事务提交成功后如 UI 更新异常，明确报告“已提交、刷新失败”，不得误报写入失败。
- 此批次 XAML 43 文件内容、MCP/Ribbon/Rebar/CSProj/安装器未改。老 `Test-ProjectAssetRC2D.py` 一条“原参数编辑仍在 UI 文件”静态断言更新为“UI 委托新 Service 且原写入 helper 仍使用”，保留检查不删除。

## 限制 / 后续

只完成源码合同与 XAML XML 解析检查，不存在 Windows .NET/Revit 实机；不得把静态校验当作实际 DataGrid 编辑成功、Parameter.Set 成功、Undo 生效。正式发行保持 NO-GO，3.1.36 FINAL_E2E 仍为稳定版本。下一开发任务 RC2F：检查其他 ProjectAssetManager 的线样式/填充图案逐格操作；逐段 Service 下沉与 GUI 静态回归，不扩业务需求。
