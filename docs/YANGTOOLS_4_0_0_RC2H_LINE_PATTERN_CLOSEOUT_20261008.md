# YangTools 4.0.0 RC2H｜线图案 ComboBox 编辑与项目资产收口审查

日期：2026-10-08。基线：RC2G，SHA-256 `1F2A58AE06E04575EDAB8CE7F1A5FFC6068CA70F651AD5AD6F547B7978CFC6E5`。

## 1. 本轮真实改动

- `ProjectAssetManagerWindow.xaml` 仅线图案 `SelectedValue` 改为 `Mode=TwoWay, UpdateSourceTrigger=Explicit`；填充图案原 XAML 不动。
- `LinePattern_DropDownClosed` 只负责捕获 PatternId/CategoryId，立刻恢复 ComboBox 原选中值；按窗口关闭、Tab/列表行、最新请求序列过滤旧回调；将写操作交给无 WPF 依赖的 `ProjectAssetLinePatternService`，绑定已有 `_doc` 和 `ExternalEvent`。
- Service 校验文档、只读、Category 和有效 LinePatternElement；兼容 `LinePatternElement.GetSolidPatternId()` 与负值内置线样式类别 ID；无变更不建事务，有修改仅一个 Transaction；检查 Start/Commit，失败回滚，回滚不可确认时明确报告。
- 模型已 Commit 却读取失败/与目标不符时返回 `ReadbackConfirmed=false`，UI 提示“已提交但回读无法确认”，**不显示写入失败**，不提前写入 ViewModel。只有回读确认后更新 UI。
- 旧 RC2G 专项测试修正硬编码“恰好 46 套件”问题为“至少 45 个原套件”，原其他约束保留。

## 2. ProjectAssetManager 收口审查（不代表运行验收）

| 区域 | 当前静态情况 | 后续结论 |
|---|---|---|
| 过滤器只读使用索引 | RC2A typed Service | 静态加固已做；运行验证延期 |
| 批量重命名 / Excel 导入 / 删除 | RC2B/C/D 已 Service 化，失败回滚规则 | 暂时收口；运行验证延期 |
| 资产名 / 参数 / ProjectInfo 编辑 | RC2E 已 Service 化 | 暂时收口；运行验证延期 |
| 线样式名称 / 线宽 / 颜色 / 图案 | RC2F/G/H 已 Service 化 | 暂时收口；运行验证延期 |
| 填充区域名称 / 遮罩 | 仍可见 UI 内直接 Transaction，错误与回滚结果检查不一致 | BACKLOG，不声称完成 |
| 填充区域前/后景图案 | XAML 仍 PropertyChanged 绑定，可能先改 VM 再执行模型写入 | BACKLOG，需改 Explicit/事务/错误恢复 |
| 填充区域颜色 | 仍从 UI 业务执行 Revit 写事务 | BACKLOG |
| 线样式与填充类型复制 | WPF 内事务，需检视级联/旧回调/Undo | BACKLOG |
| 配置加载 | 含 `catch {}` 静默降级路径 | BACKLOG，需日志与说明 |

这些遗留项先保留，**项目资产管理器结束 RC2 小批次迭代，下一轮转入 FamilyInstanceManager 的审计/最小安全改造**。正式发布前仍应补完整 UI、导入、Undo、级联删除回归，不能将静态 PASS 当作点击通过。

## 3. 兼容性约束与验收门禁

保持单 DLL、27 个原 MCP 路由契约、Revit ExternalEvent 主线程、中文 UI、Rebar/Ribbon 已验收逻辑、不主动 Git push。源文件之外的 42 个 XAML 未变；本轮只修改一个 XAML 绑定属性，需跑 43 个 XAML 静态检查。

**Windows .NET Build / Revit Loaded / 真正线图案回读 / Ctrl+Z / GUI 点击均 NOT_TESTED**。项目 `4.0.0 RELEASE NO-GO`；旧正式稳定版 `3.1.36 FINAL_E2E`。
