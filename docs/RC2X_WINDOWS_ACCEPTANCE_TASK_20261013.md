# RC2X Windows / Revit 实机自动化验收任务（待执行，不代表已 PASS）

范围：YangTools 4.0.0 RC2X 第一批。此文件给具有 Windows + Revit 的 Codex/Gemini/其他执行器使用；不要求用户人工测试；执行器的口头 PASS 不计入正式验收。

## 0. 准备与隔离

- 核实输入 ZIP SHA-256 与验收报告相同，记下载位置、机器 OS/.NET SDK、Revit 2024/2025 实际进程 PID/StartTime/Version、插件载入 DLL 文件 SHA。
- 使用临时专用测试 RVT 的副本，禁止生产工程/用户真实模型。不要覆盖已安装稳定版；先备份现有 Addins 注册文件和 DLL。
- 测试中如发生模型/文件破坏或意外崩溃，立即 STOP，保存 journal/log/截图与模型备份，不能以静态 PASS 覆盖。

## 1. C# 构建与加载（P0）

1. 分别运行工程既有 `tools/Build-Deploy-2024-2025.ps1` 所支持的构建配置，保存完整命令、exit code、stdout/stderr、产物路径和 SHA。
2. 记录实际的 Revit 2024/2025 启动进程信息、插件 addin 文件、Loaded Assembly 路径及其 SHA。必须证明装的是本批编译 DLL，而不是上批 DLL。
3. 在空白副本或专用测试项目中打开 Ribbon、图纸管理、项目资产管理、MCP 网关；记录 UI 截图、控制台异常、journal。打不开则 NO-GO。

## 2. MCP 并发/超时（P0）

- 保持原有 27 条命令、POST `/mcp/` 和 `success/result/error/durationMs` 外部字段；用既有 MCP 功能发起读操作/可控写操作。
- 分别验证 a) 请求尚在队列时超时而没有修改模型 b) 已开始在 Revit 主线程执行时的超时明确返回 UNKNOWN + operationId c) 后续真实完成的 operationId 有磁盘日志并对应模型读回 d) 完成早于超时无误报。
- 不能通过“人工睡眠 Revit UI 线程然后写模型”这类本身破坏 API 约束的测试来宣称全部通过；须保留完整时序、请求/响应、journal/模型读回。
- 遇 UNKNOWN 禁止发送重复写入请求来“试运气”。

## 3. 图纸 Excel 导入（P0/P1）

- 正常：新建一张 + 更新一张 + 两列有效参数，同一操作后图纸总数/名称/参数读回正确，一次 Ctrl+Z 全部回到操作前。
- 负向：一行不合法图号、Excel 重复图号、项目内重复图号、参数不存在、参数只读、重复同名项目参数、写入失败；任意一项发生，整个批次零变化，UI 明确错误。
- 使用空 Excel 不得产生新的 Undo。切换 Revit 文档/关闭窗口前已排队操作必须跳过，不许改写新文档。
- 逐项记录 TestCase、Before/After 的 ElementId、SheetNumber、Name、参数、Undo 后值与截图。

## 4. 项目资产：填充区域（P1）

- 编辑名称/遮罩并读回，再撤销；内置名称拒绝、空白名称拒绝、无变化不产生 Undo；窗口关闭/切 Tab/重复编辑请求不误提交。
- 进行 XAML UI 的双击编辑、复选框取消、缩放、主题切换、失效列表重载；截屏确认控件真实可用。未处理的填充图案/颜色/复制本批不得误写为 PASS。

## 5. 必交证据、GO 条件

- Build/Deploy 命令与完整日志，Revit PID/Version/StartTime，DLL Loaded SHA，MCP HTTP 原始 request/response（脱敏），Diagnostics operationId 日志，图纸/填充区域 before/after/Undo 模型读回，GUI 截图、失败截图、测试模型备份的哈希。
- 只有源码静态通过 ≠ Build GO；Build GO ≠ Loaded GO；Loaded GO ≠ 功能 Runtime GO。
- 任一关键失败不得宣布 4.0.0 RELEASE GO。自动执行环境不可用时标记 NOT_TESTED，不要求用户手测或编造数据。
