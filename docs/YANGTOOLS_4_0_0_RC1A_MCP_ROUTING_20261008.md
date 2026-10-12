# YangTools 4.0.0｜RC1A MCP Broker 分领域重构

日期：2026-10-08

## 基线、版本与发布规则

- 源码继承：`YangTools_Project_3.1.38_Source_RC0_ARCH_RELEASE_HYGIENE_STATIC_20260923.zip`。
- 输入源码 SHA-256：`EBCB76C886C725E9FAC5F52A55234B4C5ABF58DFDE0E7BD63C54C303BF9E1BC8`。
- 版本号：`4.0.0`（CSProj Version / AssemblyVersion / FileVersion / InformationalVersion；系统设置窗口的设计时版本文本）。不是 4.0 正式发布签名。
- 3.1.36 FINAL_E2E 仍是正式稳定基线；4.0.0 是首个结构重构候选。
- RC0 Windows 工程卫生验收、4.0.0 Build、Deploy、Revit 2025 Loaded SHA、模型操作均暂缓，**NOT_TESTED / DEFERRED**。
- Mimo 不再可用；用户不承担人工测试。等待未来可用 Windows/Revit 执行环境，不设无法完成的强制测试前置条件阻碍源码分支迭代。

## 范围（只改 MCP 路由内部结构）

- `src/YangTools.Revit/Mcp/RevitExecutionBroker.cs` 从 1,146 行降到约 253 行，仅负责排队、超时/取消、ExternalEvent 主线程回调、结果与状态。
- `Mcp/Handlers/RevitToolRouter.cs`：保持 27 个旧命令名对应的分发逻辑，不变更网关的 `command`/`args` 输入及 `success`/`result`/`error`/`durationMs` 输出。
- `Mcp/Handlers/CoreDocumentToolHandler.cs`：只读状态/文档/选择集/参数/元素查询。
- `Mcp/Handlers/RebarToolHandler.cs`：把原 Broker 的钢筋适配方法按原内容移动，继续调用 Core.Rebar 的现有业务 Service；不重做 GR01、R00-R10。
- `Mcp/Handlers/PythonToolHandler.cs`：保留 IronPython 运行时状态与 TransactionGroup、异常回滚；仍在 ExternalEvent 中运行。初始化失败的 LastError 状态通过委托回传。
- `McpGateway.cs`、`McpServer/server.py`、Rebar Core、Ribbon、Command、WPF 业务均不改动。
- csproj SDK 默认编译新的 `.cs` 文件；不新增第二个插件程序集，不动 Revit 2021–2027 target configurations。

## 合同保证与限制

- 输入输出格式和工具名不改；旧 MCP 客户端无需改 API 调用。
- 事务、dryRun、Undo 和读回行为未改造：静态只可证明相应源码保持，不能宣称实际 Revit 操作通过。
- Python 工具仍受网关的现有开关限制；本轮只做等价拆分，不扩大能力边界。
- 对未知命令仍返回 `不支持的 Revit 执行命令：<command>`。
- 新增 `tools/Test-McpRouterV400.py` 与 `tools/fixtures/YANGTOOLS_MCP_COMMANDS_V4.json` 固化 27 个旧命令及其领域归属。
- 原有基于 Broker 文件位置的钢筋静态测试更新为 Router + RebarAdapter 两处，避免重构后的误报；旧版本硬编码版本号的测试更新为 4.0.0。

## 独立对账

- 原 Rebar adapter 方法块与新 Handler 对比：正文逐字符一致（仅方法可见性 private -> internal 与缩进变化）。
- 原 CoreDocument 方法块与新 Handler 对比：正文逐字符一致（方法可见性与缩进变化）。
- 原 Python adapter 方法块与新 Handler 对比：除方法可见性、缩进与 LastError 回调迁移外内容一致。
- 结果在本轮 `SRC_DIFF` 报告与静态日志中提供，SHA 以最终 ZIP 为准。

## 下一阶段（与正式发布门禁分开）

1. RC2：ProjectAssetManager、FamilyInstanceManager、FamilyManager、ChineseCheck、SheetManager 的 WPF 业务逐个下沉 Service、复用 Request/Result；每项独立静态测试。
2. RC3：按 Feature 整理 Commands/UI/Models/Service 文件夹，不改现有命名空间，控制单程序集。
3. RC4：活动文档索引、归档历史验收与静态测试目录，保留证据并兼容旧测试运行入口。
4. Windows 环境可用后补 RC0 build/package、RC1 build/load、代表性 MCP read/preview；有模型后补产品链 Create/readback/Undo 等。

## 重要事实

4.0.0 RC1A 只代表源码层面结构重构完成并通过静态检查。**不等于 4.0 正式发布，也不等于整个项目已经完成 RC2/RC3/RC4 优化。**
