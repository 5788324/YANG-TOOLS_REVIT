# YangTools 4.0.0 RC2I｜FamilyInstanceManager 只读类型选项 / UI 过期请求加固

日期：2026-10-08；输入：RC2H SHA-256 `01FAF685ADE74D986485584D4D36870113C5B8206B88EAF02EFC3C43A2B210D2`。

## 约束

本批次仅调整 `FamilyInstanceManagerWindow` 的批量操作**目标类型列表读取**、`RequestRead` 队列回调及窗口生命周期，不修改批量操作 Revit 写事务、原 XAML 布局、原 MCP 27 命令、Rebar/Ribbon、单 DLL、版本号及项目配置。

## 源码事实与改动

1. 旧 `RefreshBatchOptions` 在 WPF code-behind 内分别以 `FilteredElementCollector`/`Family.GetFamilySymbolIds` 查询系统族和载入族的类型列表，且查询结果直接写 ComboBox；改为无 WPF 依赖的 `Core/FamilyInstances/FamilyInstanceTypeOptionsService.cs`，包含不可变输入快照的 typed Request/Result/Option。窗口仍进行 ComboBox 投影、错误日志和状态栏刷新。仍使用原排序规则和选择重置行为。
2. 旧 `RequestRead` 仅把闭包加入 `RevitReadEventHandler` 队列，无法区分过期 Tab/类别/族选择。新 `_readRequestVersion` 在新读取请求、标签切换、筛选置空时递增，排队回调执行前检验版本；`OnClosed` 先标记 `_isWindowClosed` 并使请求失效，再清理 ExternalEvent。
3. 新 Service 在 Revit Read ExternalEvent 调用，不创建 Transaction，也不依赖 WPF/Dispatcher、窗口或 ViewModel。保留原 `RevitReadEventHandler` 的绑定 Document 检查。
4. 原 ProjectAsset RC2H 回归测试写死“47 套件”，新增 RC2I 第 48 套后会误报；调整为“原有至少 47 套必须保留”，原其余断言全部保留。

## UI 风险与回归

- 切换载入族/系统族、频繁切 Tab、筛选清空、窗口关闭后排队的旧读取不能再主动刷新新的类型列表；**这是源代码级时序保护，未在 Revit 点击实测**。
- 保留“当前视图、当前选择、全项目”范围，批量换型/参数写入、Excel 导入导出、删除、动态列和实例两页原入口。
- 完整 XAML 原文件未修改；两个现有 DataGrid/TargetTypeComboBox/所有按钮事件未改。

## FamilyInstanceManager 尚未修复且不得宣称 PASS 的风险

- `BatchChangeType_Click` 和 `BatchSetParameter_Click` 在主事务内以 SubTransaction 继续处理剩余项，可能出现部分成功；应明确失败策略与真实 Undo 语义后再下沉 Service。
- `ImportExcel_Click` 从 ExternalEvent 内读取文件并逐行 Transaction，失败行仍可能令其他行 `TransactionGroup.Assimilate()`；需明确原子性与 UI 提示，参考但不能未经验证直接移植项目资产管理器规则。
- `InstanceDataGrid_CellEditEnding` 包含异常吞噬、早期返回和读回失败时将已提交操作视为失败的风险；需要和 Explicit Binding 一起专项重构。
- `ExecuteDelete` 使用 Trial Delete + TransactionGroup + 确认/级联路径；需要单独评估预演回滚与删除集合读回。
- 若某些写入请求在窗口关闭前入队，本批未引入全写队列统一取消语义；该项仍属于后续工作。

## 验收边界

SOURCE STATIC GO 前必须取得：旧 47 套全部保持通过 + 新 RC2I 套件、源 ZIP 全新解压复测、UI 静态检查、MCP Python `py_compile`、ZIP CRC 和中文安装器检查。Windows `.NET` / Revit Build、Loaded DLL、真实 UI/Undo 未提供证据，**4.0.0 RELEASE NO-GO**。正式稳定版仍为 **3.1.36 FINAL_E2E**。不使用 Mimo、不要求用户人工操作、不推 Git。

下一批 RC2J：优先 `FamilyInstanceManager` 逐格参数编辑的 UI 绑定/事务完整性做独立闭环；如审查发现更严重的局部提交问题，优先消除静默成功，再开展模块级收口。
