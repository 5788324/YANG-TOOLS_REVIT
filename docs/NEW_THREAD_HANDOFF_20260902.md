# YangTools / YangCAD 新对话紧急交接｜2026-09-02

> 这是新对话优先读取文档。不要从旧聊天记忆猜当前状态。

## 1. 工作方式

- Google Drive：`YangCAD_YangTools_AI_Workspace` 是主交接仓库。
- ChatGPT：架构/开发/静态测试/证据审计/文档/最终 GO-NO GO。
- Gemini：Windows/Revit/AutoCAD 执行器；不得仅凭自述判 PASS；必须交原始产物/日志/SHA/截图。
- 用户不再人工测试。Gemini 做不到的项写 NOT_TESTED，不把任务推回用户。
- 每轮实质进展结束前必须更新 `CURRENT.md / HANDOFF_LATEST / WORKLOG_LATEST`。

## 2. 正式稳定基线

### YangCAD
`YangCAD_0.9_RVT_EXCHANGE_YANGTOOLS_3136_ALIGN_STATIC_20260901.zip`
SHA `B452D326510DE664094A7B0C7170D4F07C88DAD9B59503D077DC008A3B599EB7`
状态：AutoCAD 2024 Runtime GO；RVT Exchange V1 aligned。

### YangTools 正式 FINAL
`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`
SHA `D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`
状态：Revit 2024 CAD→Revit E2E FINAL GO。

在 3.1.38 最终证据通过前，不覆盖这个正式基线。

## 3. 3.1.38 功能范围

3.1.38 合并：
1. PasteCAD Shared：Origin / Center / Shared；Shared=`ImportPlacement.Shared`；不 Acquire/Publish、不 fallback Origin。
2. Family Management V2.1：族/类型使用统计、未使用安全清理、StableParameterKey、Family→Instance、多族 RFA；实例 Scope/Type/Level、Level/Host/Pinned、批量换型/批量参数。
3. Batch Publisher V2：多模型/已打开 cloud model；NWC/IFC/DWG/PDF Export/PDF Print；逐模型资源预检、精确命名、collision、防覆盖、staging/safe replace。
4. 版权/发布层：About、版本、版权/许可、Authenticode 签名脚本。

## 4. 3.1.38 源码演进

初始统一 STATIC：
`YangTools_Project_3.1.38_Source_FAMILY_BATCH_PUBLISHER_SHARED_STATIC_20260902.zip`
SHA `73B44A36D05E019EE719855E054C18E4F79829FC1654E395B36D32FAF1BE5D4A`

Gemini 编译修复版：
`YangTools_Project_3.1.38_Source_FAMILY_BATCH_PUBLISHER_SHARED_FIX_20260902.zip`
SHA `348B3B3135607F4A9B18DA0C53C1768733002C47CC524DC608CE49E9D28D5F5C`
唯一业务修复：`InstanceIdValue.IndexOf(...)` → `InstanceIdValue.ToString().IndexOf(...)`。

当前正在其上加入版权/About/签名层；以 `00_CURRENT/CURRENT.md` 最新候选文件名/SHA 为准。

## 5. 已独立确认的 3.1.38 证据

- FIX fresh extract static：17/17 suites PASS（加入版权专项后应为 18/18）。
- Revit 2024 FIX Build/Deploy/Loaded SHA：`72C21BAF383D8F6AEBDEE89DCCB3D5CA5D95F47860BD9A63E4ED805B93A1EA7F`，真实 Revit 2024 PID 13064 load PASS。
- 单模型 `STEPP_template.rvt` 真实输出已由 ChatGPT从 Drive下载独立核验：
  - IFC 80,013,628 bytes，SHA `53C8F72CDEF65AFD9CCE5A65C3AD8ADCE42204ECC0BED57B9A874FAF934AAF06`，真实 IFC header；
  - NWC 12,898,516 bytes，SHA `922C2769D21AE752BF0D0BE6D237DD7365EA51536190097E3E97214D06CBE6F0`；
  - DWG 14,330 bytes，SHA `10F170E74BB16EADFAA1318CBF9F907F9A3281C399E9941929857230265E903E`，AC1032；
  - PDF 22,557 bytes，SHA `5B9B4FA4C8EB4E30C1B316BBF5A279F25B4445CCB95702D08AAFB5219377DFCF`，可渲染。
- Revit 2021 Print PDF：NOT_TESTED。
- Native cloud：NOT_TESTED。

## 6. Gemini 两轮证据审计发现

第一轮：把摘要 JSON 当真实输出，05_INPUT/06_OUTPUT 为空，被 ChatGPT拒绝。

第二轮：真实单模型输出已补齐，但仍有三项阻塞：
1. `Model_A_Architecture.rvt` 与 `Model_B_Structure.rvt` 实际 SHA 完全相同，只是控制副本；这可以用于队列测试，但 06_OUTPUT 仍没有任何 Model_A/Model_B 输出，所以多模型 Batch 未证明。
2. 24 张所谓 UI 截图中 ChatGPT 抽查 FamilyManager 1080×650 和 BatchPublisher 1120×720，均为 100% 纯黑像素；离屏 RenderTargetBitmap 证据无效。
3. `04_MCP_RAW` / `08_LOGS` 仍缺 Family/Batch 产品 Window→ExternalEvent→Handler/Service provenance；不能用直接 Python API primitive 代替产品链。

详见 Acceptance：
- `19_CHATGPT_INDEPENDENT_AUDIT.md`
- `21_CHATGPT_SECOND_AUDIT.md`
- `22_GEMINI_FINAL_EVIDENCE_RETEST.md`（旧 FIX 任务；版权候选封包后应生成/更新新任务）。

## 7. 版权 / 许可 / 签名最新决策

用户最终决定：**不阻止反编译，不做 DRM**。

允许：免费使用、公开分发、修改、二次开发。
要求：必须注明原始项目 YangTools、原作者 Yang、Copyright © 2026 Yang，并保留许可。
禁止：未经 Yang 授权的商业使用、销售、付费分发、收费集成。

源码新增：
- `LICENSE.md`
- `NOTICE.md`
- `THIRD_PARTY_NOTICES.md`
- About 页面正式版权/版本声明
- `tools/New-LocalCodeSigningCertificate.ps1`
- `tools/Sign-YangToolsRelease.ps1`
- `tools/Verify-YangToolsCodeSignature.ps1`
- `tools/Test-CopyrightSigning.py`

没有作者可信 CA 私钥时，不得宣称公开受信任 Authenticode PASS。自签名仅用于本地/内部签名与 Revit load smoke。私钥/PFX 永远不得进入源码包/Drive公开交付。

## 8. 新对话接手顺序

1. 读取 Drive `00_CURRENT/CURRENT.md`；
2. 读取 `HANDOFF_LATEST`、`WORKLOG_LATEST`；
3. 读取最新 3.1.38 源码候选 SHA；
4. 读取 `21_CHATGPT_SECOND_AUDIT.md` 和最新 Gemini 最终补证任务；
5. 不重新开发已经通过的 CAD→Revit 3.1.36；
6. 不把 Build 写 Runtime；不把摘要 JSON 写实际产品证据；不要求用户人工测试。

## 9. FINAL 门禁

3.1.38 只有在以下成立后才能升 FINAL：
- 当前**最新候选源码** fresh static PASS；
- Windows build/deploy/load 对最新候选闭环；签名版记录 Build SHA 与 Signed/Deploy/Loaded SHA；
- YangTools Batch Publisher 同一 run 真正处理 Model_A + Model_B，并产生两个模型前缀的实际文件；
- 真实 Windows 可见窗口截图（不是纯黑离屏图）；
- PasteCAD / Family / Batch 产品 Handler/Service 日志证明产品链；
- Revit 2021 / native cloud 若环境不安全可保留 NOT_TESTED，但不得写 PASS；
- ChatGPT 独立审计 Drive 后才可 FINAL GO。
