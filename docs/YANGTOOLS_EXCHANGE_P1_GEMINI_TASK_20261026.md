# Gemini / DeepSeek — Exchange P1 隔离验证任务

输入：最新 `YangTools_Exchange_P1_STATIC_SOURCE_20261026.zip`（以最终提供的 SHA 为准）。基线 HF2 PR #4；仅验证 CAD→Revit Exchange P1，不更改任何源码。

## 停止条件

1. 无法证明有独立 QA Revit 2025 进程、可丢弃 RVT 与非生产插件配置目录：**停止一切部署和 GUI**，只交回 F1 编译证据。
2. 发现使用当前工作 Revit 2023 或非 QA Addins：立即停止。不得自行卸载、恢复或重启生产环境。
3. 若发现编译报错，提交错误文本、日志及源 SHA，不允许 Gemini 私自修改 C# 以宣称 PASS。
4. 不修改 YangCAD 仓库，不调用工作 AutoCAD，不打开或改写生产 DWG。

## F1

- 先 Revit 2025 配置隔离 `dotnet build -c 2025 -p:Platform=x64 -p:DeployAfterBuild=false -p:RunPostBuildEvent=Never -m:1`。
- 源码 640+ 文件逐一 SHA 前后，原始日志（UTF-8/UTF-16 标记实际编码）、编译退出码、DLL 大小与 SHA。Windows 编译确实通过之后才做 F2。
- Revit 2021–2024 / 2026 编译可在 F2 真通过后一次性集中执行，不能沿用 HF2 DLL。

## F2

- 执行真正的 `CadToRevitCommand.Execute`（人工 Ribbon 与 API 外部命令应区分说明）；用隔离 MAP 和独立可丢弃 RVT。
- 逐源读回 `source_outcomes` 和 Revit `Document.GetElement(id)`，每个创建 ID 必须能在目标视图中对应到实际类型；不能用预估位置或仅看 JSON 声称创建成功。
- 特别核验 TAB-only、SPLINE、INSERT、一个对象多曲线、短线、短填充、完整填充、至少两尺寸界线及 Dimension。
- 对照 `map_raw_entries`、`reader_parsed_sources`、`reader_rejected_or_unsupported`、`mapped_sources` 与 `created_source_objects`；分辨源对象与生成子段的粒度。
- 人工 Ctrl+Z 一次并用真实 API 取 `before/after/undo`，不得用 `TransactionGroup.RollBack()` 代替。
- 报告保存失败时应区别模型已提交与报告失败。

## 证据包

原始 BUILD log / DLL / SHA 清单、Revit Journal、原始 MAP/REPORT（脱敏后保持 Handle/数值）、完整追踪 JSON、case status、原始测试脚本、真实 API 快照、QA 进程/模型/插件路径证明。不得嵌入仍有效 MCP Token。

允许中止部分用例；不接受缺失原始证据的全部 PASS。完整发布始终 `RELEASE NO-GO`。
