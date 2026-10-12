# YangTools 4.0.0｜RC2A 项目资产管理器 UI / Service 拆分

日期：2026-10-08
状态：**SOURCE STATIC GO / Windows Build + Revit GUI NOT_TESTED / RELEASE NO-GO**

## 输入与范围
- 唯一输入：4.0.0 RC1A `YangTools_Project_4.0.0_Source_RC1A_MCP_ROUTING_STATIC_20261008.zip`，SHA `3A43CA4975AE6347F66FA0800573495B212B37EBF34D04B355315973D07E67FF`。
- 限定 `ProjectAssetManagerWindow.xaml.cs` 的过滤器只读分析边界，窗口 XAML 不修改；不改线型/填充/资产删除/重命名/导入导出事务算法；不改 MCP wire contracts / Ribbon / Rebar。
- 新 `Core/ProjectAssets/ProjectAssetFilterUsageService.cs` 提供 typed `ProjectAssetFilterUsageRequest`、`ProjectAssetFilterUsageResult`、`ProjectAssetFilterUsageItem`。只允许从 Revit API 有效上下文调用；UI 通过现有 `RevitReadEventHandler` / ExternalEvent 使用。只读不启动事务。业务 Service 未来 MCP 入口可以复用，本轮未新增 MCP tool。

## 结构与性能
- 以前 WPF code-behind 为每个过滤器分别遍历视图，并将 `View` 对象缓存到 UI 字段。
- 现在 Service 按单次请求建立普通视图、视图模板两组 FilterId 索引，逐过滤器查询索引，免去 UI 常驻 Revit View 缓存；“控制实例”仍按类别过滤器+元素过滤器检查。
- 清理过期 `CheckFilterControlsElements`、`CheckFilterUsedInViews`、`CheckFilterUsedInTemplates` WPF 私有实现，保持原三个表格字段及“未使用过滤器”节点语义。
- 注意：该性能改善属于静态算法层面的推导，没有 Revit 实机速度对比，不得写性能 PASS。

## WPF UI 检查及静态修复
1. 快速切换 Tab：原异步回调执行时读取可变 `_currentTabName`，可能错误执行重复或过时的加载。新增 requestVersion + requestedTab 守卫，旧 Tab 读取任务自动丢弃。
2. TreeView 切换后已排队的读取：新增节点对象/Tab 双条件检查，避免旧选择的回调覆盖新标签页。
3. 关闭窗口后队列读取：`_isClosed` 守卫，销毁 ExternalEvent 后不再提交新操作；避免已排队回调触碰关闭窗口。
4. 状态栏显示：去掉 LoadTab 最后强行写“就绪”，保留加载数量或空列表提示。
5. 初始化和切换时：调整为 `LoadConfig -> CreateDynamicColumns -> LoadData`，减少旧列绑定读取显示新行数据的顺序风险。
6. XAML：检查五个标签页、各工具栏按钮事件、DataGrid、ComboBox 关闭提交和滚轮保护、Filter 三列绑定、资源；原 XAML 不改。模板内重复 `bg` 为不同 ControlTemplate 的局部 namescope，非错误；未误改布局。

## 自动门禁
- 新 `tools/Test-ProjectAssetRC2A.py`：解析 XAML，匹配事件处理方法，检查 UI 初始化/切换异步守卫、读写边界、Request/Service/Result、参数列、命名控件与保持现有行为，82/82 检查通过。
- 全量自动化：40/40 `Test-*.py` suites；包括既有 WPF、MCP、Rebar、Batch、Family 自动测试。
- ZIP 全新解压回归、ZIP CRC/Unicode/source hygiene、SHA 校验见 RC2A Acceptance 日志与最终源包数据。

## 限制、缺失门禁
- 此环境无 `dotnet`/Windows PowerShell/Revit：无法证明 C# 编译、XAML runtime loading、Ribbon/GUI 真正视觉效果、实际过滤器结果、ExternalEvent 时序、native Undo、载入 DLL SHA。
- 未对 Revit 2021–2027 分年进行兼容性编译；`Test-UiPanelsStatic` 的 0 Error 并非 Revit 窗口无 Bug 的证明。
- 未改变 ProjectAssetManager 其余写操作的事务结构；RC2B 再按子功能分批抽 Service，先审查导入/导出与数据编辑风险，不同时大重构。

## 下个任务
RC2B：ProjectAssetManager 的写操作与 UI 绑定继续逐功能抽 Service，并加入可独立运行的 UI/事务合同；保留现有样式与所有按钮行为，按输入源码 SHA 小批次提交。后续再 FamilyInstanceManager / FamilyManager / ChineseCheck / SheetManager。
