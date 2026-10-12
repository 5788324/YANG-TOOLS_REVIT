# YangTools 4.0.0 RC2C | ProjectAsset Excel Import / UI

日期：2026-10-08。输入 RC2B，源码静态候选，**不是实机 GO**。

## 已核对问题（旧源码）

- 原导入在单行失败时 catch 记录错误并继续下一行；只要其他行成功就 `TransactionGroup.Assimilate`。因此“完成（存在失败）”可能意味着模型部分写入，用户难以恢复预期。
- 缺失/只读参数会直接 continue，不给逐列明确信息。
- `app.ActiveUIDocument.Document` 在模型窗口切换后可能不是构建 UI 的原 Document；原 UI 回调不校验关闭/Tab/Tree，且刷新失败被外层 catch 当成模型导入失败。
- Excel 文件读入发生在 Revit ExternalEvent 的 API 操作上下文中。

## 新设计和行为

1. `ProjectAssetExcelImportRequest/Service/Result` 在 Core/ProjectAssets 独立，不引用 WPF。
2. 保留旧 Excel ID 列别名“图元 ID / 图元ID / 元素ID”和名称别名“名称 / 资产名称”。UI 首先读取并复制 Excel 行，排队执行的是不可与磁盘文件读入交错的快照（约定调用者不再修改这些行）。
3. Service 前置验证 Document、只读状态、重复图元 ID、缺失图元；无法解析的 ID 行显式统计并作为警告，不再静默；没有有效图元不启事务。
4. 使用原 TransactionGroup 作为一次 Ctrl+Z 的逻辑外边界；行事务仅在有变化时提交。某一行设置名称/参数、提交失败则向外传播原始行号和图元 ID，回滚整组；不再部分 Assimilate。无法确认回滚状态时单独警示，不谎称已经回滚。无实际修改时回滚空组。
5. 不允许把找不到或不可写的参数悄悄当成成功；对应行给出失败原因并整批回滚。此处相较 RC2B 是**有意的更严格导入语义**：含目标模型不支持列的旧 Excel 现在需要先清理列再导入，防止数据被静默丢弃。
6. UI 支持关闭、标签/节点切换和同一窗口最新导入优先的队列守卫；使用绑定 `_doc` 而非临时激活文档。成功提交和刷新 LoadData 的异常路径分离：刷新失败只显示“已提交、刷新失败”，绝不误提示“事务失败”。

## 明确没有完成

- 不改 ProjectAssetManager.xaml（43 个 XAML 文件不变），不改 Ribbon/Rebar/MCP，亦不修改其他 Excel、删除或普通 CellEdit 操作。
- 没有可用 Windows .NET/Revit，不提供 C# 真编译、真实事务/Excel/Undo/UI 点击 PASS 的虚假结果。需要未来在 Windows + Revit 中验证失败回滚、相同名称/重复 ID、空导入、只读参数和关闭窗口场景。
- 未来批次 RC2D 可继续项目资产删除/CellEdit 编辑与 UI 的单点整改，不扩大本次提交。
