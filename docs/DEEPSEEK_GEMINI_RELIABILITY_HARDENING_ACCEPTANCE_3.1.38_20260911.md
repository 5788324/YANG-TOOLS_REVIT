# YangTools 3.1.38 Reliability Hardening｜Windows/Revit 实机任务

> 本文件随源码候选交付。实际执行时以 Drive Acceptance 中绑定的源码文件名/Drive ID/SHA 为准。

## 禁止事项

- 不得修改源码后继续使用本任务的输入 SHA；发现代码 Bug 必须停下并回报。
- 不得用 Python 模拟替代 `BatchFileCommitFaultHarness` 的真实 .NET 执行。
- 不得用直接 Revit API primitive 替代 YangTools 产品入口。
- 不得把 rollback 当 Ctrl+Z。
- 不得伪造 Batch ExternalEvent；Batch 当前真实链是 Command→模态 Window→Click→Engine。

## A. Build

优先 Revit 2024。记录 build command、0 error/warning、Build DLL SHA。
其他年份按已安装环境执行，未安装/未构建如实标记。

## B. F01 文件恢复故障注入

在源码根目录执行：
`dotnet run -c Release --project tools/BatchFileCommitFaultHarness/BatchFileCommitFaultHarness.csproj`

必须 PASS，并保存 stdout。

另外通过真实 Batch Publisher 构造 overwrite success；若可安全构造提交失败，再记录旧文件 before/after SHA 与 backup path。不能安全构造则该动态项 NOT_TESTED，但 harness 必须 PASS。

## C. F02 全批预检

同一次 Batch run 加入 Model_A + Model_B：
- no subfolder
- fixed same output name
- overwrite=true

点击**执行批处理**而不是先手工点预检。

硬门禁：
- 执行入口自动 preflight FAIL；
- 新增输出文件数=0；
- 日志存在同一个 BatchRunId；
- 两模型冲突路径清晰列出。

改成 `{模型名}` 后再执行成功并保存两个模型实际文件/bytes/SHA。

## D. F03 transaction truth

Family 类型/实例参数：
- 产品 UI 修改成功时记录 Commit=Committed + model readback；
- 人为构造不可写/失败项时不得显示成功；
- 成功批次执行真实 Ctrl+Z，再 API readback 确认恢复。

TransactionGroup 的失败/回滚不能出现成功提示。

## E. F04 snapshot / busy

Batch 运行开始后：
- 输入区不可修改；
- 当前 run 的命名/格式/输出目录保持启动时快照；
- 日志记录 BatchRunId 与请求摘要。

若导出太快无法人工观察，可使用受控较大模型；不得为测试在生产代码加 sleep 后不清理。

## F. F05 provenance

上传 `%LOCALAPPDATA%/YangTools/Logs/YangTools-YYYY-MM-DD.log`。
必须能找到：
- BatchPublisher START/PREFLIGHT/EXPORT/FINAL，真实 modal chain；
- PasteCAD OperationId + ImportInstanceId；
- Family ExternalEvent OperationId + action + transaction result；
- ProcessId；
- LoadedAssemblySha256；
- document identity；
- output path / ElementId。

## G. Signing / About / Runtime

沿用版权候选规则：
- Build SHA（签名前）
- Signed Release SHA（签后）
- Deploy SHA
- Loaded SHA

硬门禁：`Signed Release == Deploy == Loaded`。
Self-signed 仅证明本地流水线，不声明公共信任。

About 用真实 Windows/Revit 可见窗口截图，显示 Version/Author/Copyright/License/Signature。

## H. UI

本轮只做受可靠性修改影响的 smoke：Batch / Family / PasteCAD 打开及关键操作无黑屏/崩溃。
全面 UI 工作区重排将在下一独立批次完成，不要在本轮擅自改 XAML。

## I. 交付

必须上传：
- build/deploy/loaded hashes
- harness stdout
- controlled input models
- actual Batch outputs + SHA
- product log
- transaction/readback JSON
- real screenshots
- final execution report

完成只写 `TEST EXECUTION COMPLETE`，最终 GO/NO-GO 由 ChatGPT 决定。
