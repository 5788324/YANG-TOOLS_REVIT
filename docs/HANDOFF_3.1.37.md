# YangTools 3.1.37 当前交接

更新时间：2026-09-02

## 1. 输入正式基线

上一正式基线：

`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`

SHA-256：

`D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`

上一基线已完成 Revit 2024 CAD→Revit E2E：

- YangCAD Export 11；
- YangTools Reader 11；
- Warnings 0；
- Bulge Arc PASS；
- MLeader Text+Leader PASS；
- Fill+Dimension PASS；
- 单次 Ctrl+Z PASS。

因此 3.1.36 FINAL_E2E 是可信正式基线。

## 2. 本轮 3.1.37 产品改动

只修改 `导入工具区 → 从CAD粘贴`：

定位方式由 2 项扩为 3 项：

1. 原点到原点
2. 中心到中心
3. **对齐共享坐标**

技术实现：

`ImportPlacement.Shared`

并加固：

- 共享坐标专用说明；
- 共享坐标失败专用诊断；
- 禁止失败后静默回 Origin；
- 成功提示显示实际 Placement；
- 日志记录 Unit / Color / Placement；
- `DWGImportOptions` 显式释放；
- settings 允许 placement index 0..2。

## 3. 本轮没有修改的业务

- CAD→Revit FINAL_E2E 主链；
- MCP；
- 线性布置；
- 材质；
- 中文检查；
- 族文档管理业务代码；
- 族实例管理业务代码。

## 4. 族管理审查

已完成源码级审查，结论见：

`docs/FAMILY_MANAGEMENT_V2_REVIEW_3.1.37_20260902.md`

核心结论：

- 族文档管理当前实质是“载入族/族类型表格管理”；
- 族实例管理当前实质是“实例参数表格管理”；
- 两窗口 code-behind 已分别约 1209 / 1258 行，但高频管理动作偏少；
- 建议下一轮单独做 Family Management V2.1，不在本轮盲目扩展。

## 5. 静态状态

新增：

`tools/Test-PasteCad.py`

结果：

`19/19 PASS`

全量：

`tools/Test-*.py = 15/15 suites PASS`

UI：

- Ribbon 31；
- XAML/资源/事件：0 error / 0 warning。

## 6. 当前判定

`STATIC GO / WINDOWS REVIT RETEST REQUIRED`

本轮产品源码发生变化，因此 3.1.36 DLL 不能作为 3.1.37 运行证据。

## 7. Windows/Revit 下一步

执行：

`docs/DEEPSEEK_GEMINI_PASTE_CAD_SHARED_ACCEPTANCE_3.1.37_20260902.md`

必须至少覆盖 Revit 2024：

- Origin 回归；
- Centered 回归；
- Shared 成功案例；
- Shared 失败不 fallback；
- 一次 Ctrl+Z。

## 8. 文档纪律

每次实质进展后先更新 Google Drive：

- `00_CURRENT/CURRENT.md`
- `HANDOFF_LATEST`
- `WORKLOG_LATEST`

源码新版本同时同步 README / PROJECT / WORKLOG / HANDOFF / FEATURE_AUDIT / COMPATIBILITY。
