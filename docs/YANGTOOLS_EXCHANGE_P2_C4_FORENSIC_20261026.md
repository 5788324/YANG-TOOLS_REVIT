# YangTools 4.0.0 Exchange P2 — C4 原工程失败诊断与 HATCH polyline 修复

日期：2026-10-26。父基线：Exchange P1，Git commit `99d493b99473ddcaebefc412ec031bf80655ad82`，642 文件，F1 2021–2026 BUILD GO / F2 Revit 2025 限定 GO。

## 已经核实的历史证据（不是本轮新实机结果）

- C4 源 MAP 名称：`D10-AEC-DRW-PLN-STR-00-Z00-10006_RVT_MAP.json`，SHA-256 `A0AB7DFEE336919EC5AAF49B4C7D56ACC708165ED525CD07230CC065397F5339`；此文件本身未包含在当前交接包中。
- YangCAD 旧 `sample_report_summary.json`：扫描 968，分类 841，HATCH 71；C4 回读的 Revit 交换对象计数为 835，其中 Fill 65。**差额 6 发生在两个不同统计阶段，不足以推出恰好丢失 6 条 polyline HATCH。**
- Revit Fills：65 输入、46 创建、19 失败：`1D,1F,20,25,27,29,2A,B6,C3,148,152,1FB,1FD,1FE,276,28C,29C,2AC,2B3`；Dimensions：58 输入、57 创建、Handle `1249` 失败；原始失败日志记录 `Curve length is too small for Revit's tolerance`。历史报告里的 bbox/位置“Handle 匹配”不是源到目标的精确创建记录。
- C4 同批历史 Join 问题：ElementId 526908/526932/526935 对应历史 CAD `CB`（ARC）、`158`（ARC）、`15B`（LINE），两次 `Can't keep elements joined`；历史日志不是完整可重放工程快照。

## P2-01 — 新确认的 YangCAD / YangTools 协议兼容缺陷

YangCAD `RevitExchangeService.GetHatchLoops` 可以输出 `kind="polyline"` 且 `vertices=[{point:[x,y],bulge:...}]` 的 Hatch 边界；P1 `CadRevitExchangeReader.TryParseFill` 仅处理 `loop.curves`，其余调用 `ReadPointList(loopToken)`，但 `ReadPointList` 要求传入 `JArray`，而此处 `loopToken` 实际是 `JObject`。由此，直线 polyline 填充也会被整环拒绝。

P2 修复：显式解析 polyline `vertices`，验证顶点个数和坐标、禁止非零 bulge（当前 Revit V1 不能静默将弧线变直线），仅将**全直线** Hatch 交给原有 `BuildCurveLoop` 的长度、闭合和事务检查。原 MAP 不修改。不会将 Revit 拒绝的 0.78mm 以下线段悄悄丢弃。

**本轮不能宣称 19 个填充已恢复**：其中至少历史日志显示 19 个在短曲线创建环节失败，和读取 polyline 的缺陷不是一回事。仅在获取原 MAP 后，才能证明差额 6 与 polyline 的关系。

## P2-02 — 只读 MAP 精确诊断工具

`tools/diagnose_exchange_map.py`（纯 Python 标准库）：

- 读取 MAP，计算源 SHA-256；可选 `--expect-sha256` 严格匹配 C4 样本，防止对错文件下结论。
- 以实测 Revit 2025 的 `ShortCurveTolerance=0.7803686370624999mm` 为默认阈值（**仅属所测 2025 宿主，不代表所有版本/模板**），按 Hatch Handle/loop/edge 给出最短边、短边长度和弧形顶点；按 Dimension 记录可读取的尺寸界线情况。
- 支持 `kind=curves`、`kind=polyline` 及常见点数组；不可解析的轮廓明示 `unsupported`，不会补形、自动闭合或替换曲线。
- 默认**不导出 CAD 坐标**，以便把诊断 JSON 在团队间传递；它只是一种预检查，不是 Revit Runtime 的恢复证明。

### Gemini / DeepSeek 下一次需要做的最小任务（不运行 Revit 或 AutoCAD）

在已授权的工作目录中找到既有 `D10-AEC-DRW-PLN-STR-00-Z00-10006_RVT_MAP.json` 副本，不扫描/更改企业的其他 DWG。复制到**独立可丢弃 QA 目录**并确认 SHA 后运行：

```powershell
py -3 tools\diagnose_exchange_map.py "<QA_MAP_完整路径>" `
  --output "<QA_输出目录>\C4_MAP_DIAG_P2.json" `
  --tolerance-mm 0.7803686370624999 `
  --expect-sha256 A0AB7DFEE336919EC5AAF49B4C7D56ACC708165ED525CD07230CC065397F5339 `
  --historical-c4
```

若原 MAP 的字节哈希已因合法重导出发生变化，**先不要绕过哈希强行对账**：记录实际 SHA，另行运行不带 `--expect-sha256` 的检查，并把结论标成“另一代样本”。

必须提交原始 MAP SHA（若不能分享原 MAP，可提交脱敏但几何长度保真的最小 Handle/环/段报告）、诊断 JSON、运行命令、退出码、当前 YangCAD Draft PR #3 生成的 MAP/REPORT 精确计数（如另行做 CAD QA）及 Revit P1 已输出的原始逐 Handle 失败报告。

## 验收边界

- 静态契约与诊断回归 ≠ Revit 插件编译成功；不得使用旧 P1 的 DLL 证明 P2 编译。
- 用户生产 Revit 2023、其他工作中的 AutoCAD/Revit、RVT/DWG、原 Addins 不可被此诊断任务修改。
- 如需后续合法重建短边、曲线 HATCH 或原生径向标注，必须基于原始 MAP 的真实形状及实际失败日志另开小任务，先复核形状再实现，不能用 bbox 事后推断替代真实 Handle 映射。
- 发布门禁维持 Release NO-GO；P2 Source STATIC GO 只能在静态回归通过后声明。
