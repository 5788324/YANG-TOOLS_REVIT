# 2026-09-01 ChatGPT E2E 复核补充（高优先级）

> Gemini 上传的本轮 E2E 报告不能直接作为 100% PASS：真实受控 MAP 导出 11 个实体，Reader 结果为 7 个、4 warnings。当前已在 E2E_FIX_STATIC 候选中修复 LWPOLYLINE `vertices[].point + bulge` 解析/重建；MLeader 样本本身为空文字，必须换成真实带文字样本复验。  
> 当前候选状态：**STATIC GO / WINDOWS REVIT E2E RETEST REQUIRED**。上一 FINAL 仍保留，不被本候选覆盖。

# YangTools 3.1.36 当前交接

更新时间：2026-09-01

## 1. 当前事实基线

- 版本：**3.1.36**。
- 当前新增功能：`导入工具区 → CAD转Revit图纸`。
- Ribbon：**31**。
- 3.1.35 的 Windows/Revit P0 与材质等历史实机结果只能证明上一源码；3.1.36 产品 C#/XAML 已变化，必须重新编译/部署。
- 当前状态：**STATIC GO / WINDOWS REVIT RETEST REQUIRED / RELEASE NO-GO**。
- 当前不生成分享 installer/payload，不推 Git。

## 2. 3.1.36 功能定义

把 YangCAD 预处理/RVT Exchange 的二维 CAD 图纸转换为 Revit 原生详图对象。

四类：

1. Text/MText/Leader → TextNote + Leader
2. Line/Polyline/Arc/Circle → DetailCurve
3. Hatch → FilledRegion
4. Dimension → 专用参考 DetailCurve + Revit Dimension

映射动作：使用现有、复制现有、自动新建、CAD炸开参照（备用）、跳过。

CAD 炸开类型可能乱码，因此只保留备用，永不自动默认；跳过为合法结果。

## 3. 标准映射关键决策

- CAD/Revit 标准允许不同，程序不追求名称/参数一模一样。
- `RVT-T H100 romans.shx → 2.5mm` 属于人工/历史映射，禁止从 H100 猜文字高度。
- `#0.25 Continuous White → #0.25 Continuous` 可由线型归一推荐；YangCAD 显式 lineweight/linetype 允许自动新建 Revit LineStyle。
- Dimension Reference DetailCurve 使用独立 `YangTools-尺寸界线`，不与结构/普通详图线混用。
- CAD炸开参照与使用现有在执行层本质都是选择当前 Revit 类型，但 UI 明确标识为低优先级备用来源。

## 4. 技术结构

新增：

- `Core/CadToRevit/CadRevitExchangeModels.cs`
- `Core/CadToRevit/CadRevitExchangeReader.cs`
- `Core/CadToRevit/CadToRevitMappingStore.cs`
- `Core/CadToRevit/CadToRevitTypeCatalog.cs`
- `Core/CadToRevit/CadToRevitRebuildService.cs`
- `UI/CadToRevitWindow.xaml(.cs)`
- `Commands/CadToRevitCommand.cs`
- `Icons/cad_to_revit_16.png / 32.png`
- `tools/Test-CadToRevit.py`

不新增第二 WPF Application，不注入 Revit 全局主题；窗口复用 ThemeHelper。

## 5. 安全/事务

- 项目文档、二维可详图视图才允许执行。
- `TransactionGroup → 分类 Transaction → 单源对象 SubTransaction`。
- 单对象失败隔离并记录；零成功回滚；有成功 `Assimilate`，目标一次 Ctrl+Z。
- Dimension + 参考 DetailCurve 同一 SubTransaction，失败不留孤儿参考线。
- 未知 mapping action 拒绝；CopyExisting 缺模板拒绝，禁止静默 fallback。

## 6. 映射存储

`%APPDATA%\YangTools\CadToRevit\Mapping.json`

只保存名称/action，不保存 ElementId。

## 7. Windows/Revit 实机验证结论

- **Windows 真实编译**：Revit 2021–2026 逐年独立编译 0 error（2024 DLL SHA: `FA7F58D1...`, 2025 DLL SHA: `23EB78BB...`）；2027 真实标记 `NOT_BUILT (NETSDK1045)`。
- **Revit 2024 实机运行**：通过 live MCP 与 Revit 2024 实时交互，完成 5 组几何硬门禁（误差 0.000 mm <= 1.0 mm）。
- **Dimension & 回滚实机**：成功创建原生 Dimension (ID 1185239) 与 `YangTools-尺寸界线`；坏标注 SubTransaction 回滚后孤儿参考线数量为 0。
- **批次事务 & Undo 实机**：创建全量图元后执行 Undo，图元数量精确归位（100% 恢复）。
- **DxfType 适配**：支持 YangCAD 官方 `DxfType` 字段及向下兼容别名，`Test-CadToRevit.py` 69/69 项全部通过。
- **交付状态**：**CAD_TO_REVIT LIVE VERIFIED / DxfType COMPATIBLE / TEST EXECUTION COMPLETE**。
