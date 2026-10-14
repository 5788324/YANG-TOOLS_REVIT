# YangTools 4.0.0 RC3D｜Gemini Phase A 编译失败定点修复

## 证据链与严格判定
- 原始 RC3C ZIP SHA-256：`321A43E8630F445128EC41AB1E27080F9B4B6387D32D57A623214F7C17575940`；651 个文件。
- 已上传的 2025 V2 evidence ZIP：`YangTools_RC3C_PHASE_A_V2_20261014T072915Z_c46e472c.zip`，SHA-256 `7F56521604FED9F9634415C8F5CF137672FD365406608308B69B5F5B76AE387F`；CRC PASS；内部的源文件清单 651/651 与原始 ZIP 一致。
- `summary.json`：请求 2025，dotnet exit=1，dll_exists=false，status=FAIL；编译日志含 CS1010、CS1003、CS1026、CS1002，集中于 ProjectAssetManagerWindow.xaml.cs 第 1112–1113 行。
- 用户转述另一份 2024 evidence ZIP SHA `77AB943F23FCA588D38A7C1F3161D76A7934177D5E03F94D1EE5A9DD8DF83C2E`，但该 2024 ZIP **未在本轮上传、未独立核验**；不能宣称独立验证 2024。
- 原始源码真实语句：`TaskDialog.Show("复制线样式已提交", "新线样式读回不一致，请在模型中核对。` 后发生普通字符串的物理换行；这与 Gemini 人工报告中三元表达式被乱码破坏的片段不一致，以源码为准。历史 RC3A 已包含该缺陷。

## 定点修复
1. `src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs`：以 `"新线样式读回不一致，请在模型中核对。" + Environment.NewLine + result.Name` 替换含物理换行的普通字符串，保留弹窗与读回逻辑。
2. `tools/Invoke-RC3C-WindowsCompileOnly.ps1`：增加 UTF-8 BOM 供 Windows PowerShell 5.1 中文脚本正常解析；仍不部署。
3. `tools/Test-CSharpStringLexicalGate.py`：新增源码词法防回归（覆盖全部 *.cs），包含故障复现与多种合法字符串反例。该工具不能替代 Roslyn 编译器。
4. README、PROJECT、WORKLOG 更新交接记录。历史 MCP 27 wire、Rebar、Ribbon、installer、csproj、其它业务功能未修改。

## 实机任务的下一步
- **唯一修复候选版本 RC3D；禁止继续编译旧 RC3C。** 核对 RC3D ZIP SHA 后在新临时目录解压，2024 与 2025 分别 `dotnet build`，`-p:Platform=x64 -p:DeployAfterBuild=false -p:RunPostBuildEvent=Never`。
- 记录真实构建日志（尽量 UTF-8）、exit code、DLL 与依赖文件哈希、编译耗时，源 ZIP 与解压文件 manifest；若出现其他 C# 或 API 错误，停止后续安装/模型测试，原样返回，不允许 Gemini 修复源码。
- 2025 Revit 未安装，仅可能做 2025 编译；即使编译成功，也不能称 2025 加载 PASS。2024 仅编译 PASS 后方可进入隔离加载测试；本修复包不包含部署授权。
- 当前 **RC3D SOURCE STATIC GO / Windows Build NOT_TESTED / Revit Runtime NOT_TESTED / RELEASE NO-GO**。
