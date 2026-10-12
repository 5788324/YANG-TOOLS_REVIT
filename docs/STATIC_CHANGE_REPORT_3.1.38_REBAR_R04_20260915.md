# Static Change Report｜YangTools 3.1.38 Rebar R04

日期：2026-09-15
状态：**STATIC GO / REVIT RUNTIME REQUIRED / RELEASE NO-GO**

## 变更

1. 新增 `RebarShapeDrivenModifyModels.cs`。
2. 新增 `RebarShapeDrivenModifyService.cs`：Shape Driven 原位 BarType / NumberWithSpacing 修改，提交前验证 ElementId / UniqueId / Host / Constraint signature。
3. 新增 Ribbon：`钢筋工具区 -> 原位修改 R04`。
4. MCP Broker + Python server 新增 typed tool `modify_shape_rebar`，默认 dryRun。
5. R00 Inspector 升至 `R00.3`：sample bar position 增加 transformed Start/End/Midpoint 和 GeometryMode；通过反射优先调用 `GetTransformedCenterlineCurves`，保留旧版本 fallback。
6. 新增 `Test-RebarR04.py`。
7. UI 预期 Ribbon 数从 35 更新为 36。

## 静态结果

- R00: 44/44 PASS
- R01: 35/35 PASS
- R02: 44/44 PASS
- R03: 53/53 PASS
- R04: 50/50 PASS
- UI: 172 C# / 42 XAML / 36 Ribbon / 0 error / 0 warning
- 全部 `tools/Test-*.py`: 24 suites / 0 failure

注意：静态检查不能证明 Revit API runtime，尤其是 `GetTransformedCenterlineCurves` 的跨版本反射路径、ChangeTypeId、layout update 后 constraint/varying 保持与 Undo，必须按 Mimo R04 任务实测。
