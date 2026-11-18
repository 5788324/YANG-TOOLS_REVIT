# YangTools PersonalPilot — VersionGate REGEX_FIX 独立证据审查

日期：2026-11-18。历史审查状态：**CLI LIFECYCLE PASS / TARGET-VERSION NEGATIVE GATE PARTIAL**；已由下文补充验收更新。

## 2026-11-18 补充验收（最终裁定）

**CLI LIFECYCLE GO / TARGET-VERSION NEGATIVE GATE GO（凭用户 Windows QA 证据） / PERSONAL PILOT CLI ACCEPTED / GUI 安装回退点击 NOT_TESTED / FORMAL RELEASE NO-GO。**

用户新上传完整证据 `VERSIONGATE_REGEX_FIX_EVIDENCE(1).zip`，**9,724 bytes**，SHA-256 `55418B30C07D5D650981AFAC5AB9BD24A6354BF77CA2382739A8B80E0E8AAC8E`，压缩包 10/10 CRC PASS，清单 9/9 SHA-256 PASS。原始文件 `step4_negative_gate_subprocess.json` 和 `scripts/run_step4_supplemental_subprocess.ps1` 已直接检查；Windows 子进程没有在审查环境重跑。

- 在生产 Revit 2022（PID 17776）及 QA Revit 2025（PID 8476）均运行时，独立 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` 调用 `Install.ps1 -Versions 2025`，记录 **ExitCode = 1**，stdout 明确为“安装失败：目标 Revit 2025 正在运行（PID 8476）”，stderr 为空；DLL、manifest 原 SHA 不变，`v2_marker.txt` 未写入，`unchanged=true`。
- 同一隔离环境调用 `Uninstall.ps1 -Versions 2025`，记录 **ExitCode = 1**，stdout 明确为“卸载失败：目标 Revit 2025 正在运行（PID 8476）”，stderr 为空；DLL 和 manifest SHA 不变，`unchanged=true`。
- 旧 `step4_negative_gate.json` 的 `install_blocked=false`、`uninstall_blocked=false` 属于原验收脚本未检查子脚本 `exit 1` 的测试缺陷；**不改写原始证据**。本次独立子进程的退出码与文件读回已补足那两项验收。
- 旧 `step3_cli_lifecycle.json` 已展示 2025 首装→更新→回退→卸载、个人文件 SHA 始终不变；旧反向记录中 `rollback_blocked=true`、目标 PID 提示正确。CLI 阶段可冻结。
- 限制：v1 与 v2 测试 **DLL 的 SHA 相同**，不同版本 DLL 的实际回退尚未验证；`unknown_path_rejected_by_regex` 只测试正则匹配，中文 GUI 安装/回退按钮并未实测点击。没有证据支持正式 4.0.0 完整发布或对生产插件进行安装。
- 冻结当前安装器源码，不新增 P12/新修复分支；如果要交付个人试用包，下一项只做中文 GUI 安装/回退的最短实机点击验收，仍隔离生产环境。

---

## 2026-11-18 原始首轮证据审查（历史保留）

## 证据来源与完整性
- 输入：用户上传 `VERSIONGATE_REGEX_FIX_EVIDENCE.zip`。
- 文件大小：**7,116 bytes**。
- SHA-256：`50A4973DF5E6C2450B01529B181374C4E34B9ECC0558588048C7BBD5F87EC57B`。
- 压缩包：**8/8 CRC PASS**。`SHA256SUMS.txt` 所列 **7/7 SHA-256 PASS**。
- 直接审阅：`ACCEPTANCE_REPORT_VG_REGEX_FIX.md`、`ps51_parser_check.json`、`step3_cli_lifecycle.json`、`step4_negative_gate.json` 和全部 3 份 `scripts/*.ps1`。证据包不包含真实 `Revit.exe` 或 DLL；因此未独立复跑 Windows 命令，结论仅限文件、数据、脚本逻辑的独立检查。

## 已有充分文件状态证据的结果
1. 五份 `installer/*.ps1` 的 PowerShell 5.1 解析记录均为 0 错误、UTF-8 BOM 为 true。
2. 在隔离 `%APPDATA%` 中，`Install.ps1 -Versions 2025` 的 v1 首次安装记录 DLL 与 manifest；无 backup；用户文件 SHA 保持 `2E8FEFB9BE2DAE20AA23478C944E1CFC9001DD13A8708565E4165519952C715C`。
3. 第二次安装（v2）留下 `v2_marker.txt`，manifest SHA 改变至 `77A7AB...`；旧 manifest 与 DLL 被保存在对应 backup。该试验 v1/v2 **DLL SHA 相同**，因此能证明目录与 manifest 的版本交换，但不能证明不同 DLL 二进制的实际回退。
4. 单次 `Rollback.ps1 -Version 2025` 后标记消失、manifest 恢复到 v1 SHA、v2 转为备份，用户文件 SHA 不变。
5. `Uninstall.ps1 -Versions 2025` 后插件、manifest 及备份路径不再存在，用户文件仍在且 SHA 不变。
6. 2022 进程 PID 17776 出现在生命周期记录中，没有导致隔离 2025 流程失败。证据的状态快照互相一致。

## 关键不一致：报告声称三项反向门禁均通过，但原始 JSON 不支持
`step4_negative_gate.json` 的原始字段为：

| 检查 | 原始字段 | 结论 |
|---|---|---|
| Revit 2025 运行时拒绝安装 | `install_blocked=false`, `install_error=""` | **INCONCLUSIVE** |
| Revit 2025 运行时拒绝回退 | `rollback_blocked=true`，含正确目标 PID 提示 | **PASS（该项证据）** |
| Revit 2025 运行时拒绝卸载 | `uninstall_blocked=false`, `uninstall_error=""` | **INCONCLUSIVE** |
| 既有 DLL 未被覆盖 | `dll_unchanged=true` | **PASS（文件状态）** |
| v2 标记未被写入 | `v2_marker_not_written=true` | **PASS（文件状态）** |
| 未知路径拒绝 | `unknown_path_rejected_by_regex=true` | **仅正则不匹配测试；未证明完整函数抛错** |

检查 `scripts/run_step4_negative_gate.ps1` 发现：它通过 `try { & Install.ps1 ... 2>&1 | Out-Null } catch { ... }` 及相同方式调用 Uninstall。**但安装、卸载脚本内部的 `catch` 会输出错误并 `exit 1`；外层的 PowerShell try/catch 未检查子脚本退出码**。因此 `install_blocked=false`、`uninstall_blocked=false` 可能只是测试方法无法识别失败，不能据此断定门禁失效；同样也不能把它们直接判为 PASS。

## 最小补证建议（不改产品）
1. 保持生产 Revit 2022 不动，在**已隔离的 QA `%APPDATA%`** 环境启动 Revit 2025 QA。
2. 将 `Install.ps1 -Versions 2025` 与 `Uninstall.ps1 -Versions 2025` **各自放入独立 `powershell.exe -NoProfile -File` 子进程**运行，捕获每次 `ExitCode` 和完整 stdout/stderr，不能只用父进程 `try/catch` 的布尔值。
3. 预期两次都是非零退出且包含“目标 Revit 2025 正在运行”，并在两次调用前后对 QA Addins 目录的 DLL/manifest、标记文件读取相同 SHA/状态。
4. 由于回退已收到真实失败提示，仅在需要时保留该项原始输出；不要重新编译、重新测试 C4 或改变 PowerShell 业务代码。

**目前可以冻结个人试用版的隔离 CLI 安装、更新、回退、卸载正向流程；完整安装器拒绝覆盖的反向验收须等两项退出码补证。** GUI 选项卡与窗口加载曾在上一轮通过，但本证据包不证明图形界面里的安装/回退按钮执行全链路。GitHub 保持 Draft，不合并 main。