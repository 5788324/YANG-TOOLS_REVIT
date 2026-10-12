# YangTools 3.1.37 静态变更与审查报告

日期：2026-09-02

## 输入正式基线

`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`

SHA-256：

`D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`

## 产品代码变更

仅涉及“从 CAD 粘贴”：

- `src/YangTools.Revit/Commands/PasteCadCommand.cs`
- `src/YangTools.Revit/Core/UserSettings.cs`
- `src/YangTools.Revit/UI/PasteCadWindow.xaml`
- `src/YangTools.Revit/UI/PasteCadWindow.xaml.cs`
- `src/YangTools.Revit/YangTools.Revit.csproj`

测试：

- 新增 `tools/Test-PasteCad.py`
- `tools/Test-CadToRevit.py` 版本契约更新为 3.1.37

族文档/族实例管理只审查，未改产品代码。

## PasteCAD 新能力

定位方式：

1. 原点到原点
2. 中心到中心
3. 对齐共享坐标（`ImportPlacement.Shared`）

安全语义：

- 不 Acquire/Publish Coordinates；
- 不修改项目坐标；
- API 失败不静默 fallback Origin；
- 不自动宣称 DWG 与 Revit 工程坐标语义一致；
- Shared 最终正确性必须用 Revit 原生 Shared 或控制点/Transform 对账。

## 代码审查结论

现有 PasteCAD 主链无需重构：

- AutoCAD `A$C*.dwg` 候选检测；
- 手动 DWG；
- 工作副本与重试；
- finally 清理；
- 单 Transaction；
- Import false/exception rollback；
- 成功后选中 ImportInstance；
- 一次 Ctrl+Z。

`ThisViewOnly` 仅在 3D 禁用与 Revit API 契约一致。

未发现新的 P0 源码问题。

## 静态测试

最终源码状态：

- `Test-PasteCad.py`: 19/19 PASS
- `Test-CadToRevit.py`: 76/76 PASS
- 全量 `tools/Test-*.py`: 15/15 suites PASS
- UI：148 C# / 42 XAML / 37 roots / 4 themes / 31 Ribbon
- UI errors: 0
- UI warnings: 0
- C# 轻量词法/括号扫描：148/148 PASS

## 族管理审查

见：

`docs/FAMILY_MANAGEMENT_V2_REVIEW_3.1.37_20260902.md`

核心结论：当前两个窗口业务价值低于代码体量，应优先做 V2.1 高频管理动作和 ParameterKey 加固，不建议继续往 code-behind 直接堆按钮。

## 当前判定

`STATIC GO / WINDOWS REVIT RETEST REQUIRED`

不得把 3.1.36 DLL 当作 3.1.37 运行证据。
