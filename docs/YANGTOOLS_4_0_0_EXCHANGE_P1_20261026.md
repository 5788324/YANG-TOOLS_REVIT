# YangTools 4.0.0 — CAD→Revit Exchange P1 数据完整性

日期：2026-10-26。基线：RC3J-HF2，Git Commit `31a091dabd4b67cf1de5293357b7c75555fa6b68`，638 文件，Git Tree `f45150fcd2f37223ff15e69a1d4ca4d3f1500e9a`，源 ZIP SHA-256 `9923FD9E47D4E6EB212A09EF6FF05C5BFCE4EAA154FB40670D5AF0542EFFD5B7`。

## 目的与修改边界

只修改 YangTools 内的 `Core/CadToRevit/`、`CadToRevitCommand` 的说明/结果提示以及相应测试，不改 YangCAD 仓库、不升级 `project.revit.prepare`、不新增 MCP wire、Ribbon 命令或图标，也不改 HF2 已通过限定验收的三项 2D 整理工具。

| 编号 | 本轮变化 | 没做的事情 |
|---|---|---|
| EX-01 | 从实际 `document.Application.ShortCurveTolerance` 获取阈值，在线、尺寸界线、尺寸线、填充直线环、Polyline/圆弧创建前检查。保留精确第几环/第几段失败原因。显式首尾重复点仅按原坐标闭合标记处理 | **没有**自动合并短边、平滑、缩短、闭合修复；不能声称原 19 个 HATCH 已全部恢复 |
| EX-02 | Reader 和创建阶段都拦截 TAB-only / 全空白 / 纯控制字符文本，不再把它计入成功 | 没有变更 MText 排版和字体替换 |
| EX-03 | 每个来源对象记录 `SourceId`/Handle、kind、`created/skipped/failed/rejected/unsupported/duplicate`、原因；创建成功的对象在子事务提交后登记 **真实** ElementId，标注附属尺寸界线按 role 单独记录。一次 CAD Polyline 可以映射多条 Revit Curve | 不是根据几何 bbox 事后猜 Handle；报告本身不是外部 CAD 数据数据库 |
| EX-05 | 区分 MAP 原始条目（包括 unsupported、重复）、reader 识别对象数量、映射执行对象数、创建的源对象数、实际 Revit 线段数；不把线段数混作源对象数 | **不**伪称掌握源 AutoCAD DWG 扫描的全部对象；对于已知 `968/841/835` 差异仍需原始 MAP/REPORT 比对 |
| HATCH 限制 | 对 `curves` 边界仅接受可证明连续、闭合的 `LINE`，拒绝非直线或缺端点环，记录 Handle | 不把 ARC/SPLINE HATCH 伪装为直线轮廓 |
| 不支持 CAD 对象 | `SPLINE/INSERT/VIEWPORT/PROXY` 明确记 `unsupported`，不静默展开 | 不支持这些对象的原生重建 |

## 逐源审计报告

模型事务组真正 Assimilate 完成后，写入用户本机：

`%LOCALAPPDATA%\YangTools\CadToRevit\Reports\exchange_YYYYMMDD_HHMMSS_<唯一ID>.json`

JSON schema：`yangtools-cad-revit-audit/1.0`。内容包括 MAP `source_map_path`、坐标单位、origin、实际 ShortCurveTolerance（英尺/mm）、分阶段计数、每个源 Handle 的 `source_outcomes[]`、`ElementId/Role` 和具体失败原因。用唯一文件名和临时文件原子落地，不覆盖 DWG / MAP。全部对象转换失败时即使模型事务组回滚也尽力留存报告。报告保存失败不能把已提交的模型误报为未提交；弹窗会给出警告。

状态 `created` 只表示 Revit 子事务提交成功，不保证外部图纸语义（样式/视觉、尺寸）全部符合原 CAD；需要 QA 复核。

## Revit 运行风险 / 尚未解决

1. `FilledRegion.Create` 的原生 CurveLoop 拓扑/自交/嵌套限制仍须实机测试。短边拒绝仅消除无记录的变形风险，不会提高这类数据的创建率。
2. `Arc.Create` 非平面、过小弧、跨度等限制仍需 Revit 2025 测试；保守预筛可能拒绝极端短弦大弧。
3. 两次 `Can't keep elements joined` 的原始根因尚缺完整 Journal/原始输入，不能归因于坐标 origin 或本次修改。
4. YangCAD PR #3 的 `coordinate_origin_mode=local` 为独立候选；YangTools 沿用一次 `(Data-origin)`，**不二次平移**。
5. 非本次处理：2027 .NET10、跨年 Runtime、真 Ctrl+Z、所用模板中的字体/标注排版误差、HATCH 非直线轮廓重建。
6. Windows C# **BUILD NOT_TESTED**，Revit **Runtime NOT_TESTED**。现有 HF2 编译/运行通过证据不能迁移为本候选 PASS。整体 Release NO-GO。

## 验收指引

静态：`python tools/Test-CadToRevit-IntegrityP1.py`、`python tools/Test-CadToRevit.py` 及 HF2 UI/Geometry 回归。

Windows：只从本候选源码 ZIP 新目录构建 Revit 2025。收集源码前后 SHA、原始日志、DLL SHA。2025 编译通过后才允许在独立 QA 帐户/非生产模型验证：

- TAB-only Handle：Reader `rejected`，创建计数不增加。
- 正常 TextNote：`created`，指向实际 TextNote ID。
- 短详图线：`failed`，含 SourceId、Revit 阈值；其他长线正常创建。
- HATCH 含短边：整条 SourceId 失败，无部分填充；直线闭合 HATCH 成功（如真实 Revit 允许）。
- HATCH 曲线边：`rejected`，不伪造多边形。
- 普通 Polyline：一个 SourceId 可映射多条 ID。
- Dimension：被创建 Dimension 与附属 ReferenceLine 的 ID/Role 分列；失败整段子事务回滚。
- INSERT/SPLINE：`unsupported`，总账不会无声消失。
- 一次真实 Ctrl+Z 及负向零创建回滚；提交前后 API 读回；原始 Journal。
- 使用同一 CAD 输出 MAP 与 YangCAD `REPORT` 比较 Handle、原点、空间计数和类别；原 DWG 前后 SHA 不变。

**特别约束**：Revit 2023 当前用户工作环境及现有 Addins 不得操作/部署/重启；任何测试者的自然语言 PASS 不代替原始日志、Revit API 快照或源文件哈希。
