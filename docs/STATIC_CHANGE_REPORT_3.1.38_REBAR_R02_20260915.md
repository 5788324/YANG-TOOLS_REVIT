# YangTools 3.1.38 Rebar R02｜静态变更报告

日期：2026-09-15
父候选：`YangTools_Project_3.1.38_Source_REBAR_R01_STATIC_20260915.zip`
父 SHA：`94A32D1C498AFC5A6078C2B0817BDE415367F0C84158FE342206478F0CC9A2E4`

## R01 Runtime 结论

依据 Mimo R01 原始结果包独立复核：`R01 CORE RUNTIME GO / UI+UNDO OPEN / RELEASE NO-GO`。

详见 `R01_MIMO_RESULT_REVIEW_20260915.md`。

## R02 新增

- `Core/Rebar/RebarFaceDrivenCreateModels.cs`
- `Core/Rebar/RebarFaceDrivenCreateService.cs`
- `Commands/RebarPlanarFaceSelectionFilter.cs`
- `Commands/RebarFaceDrivenCreateCommand.cs`
- MCP：`inspect_rebar_host_faces`
- MCP：`create_shape_rebar_on_face`
- `tools/Test-RebarR02.py`
- Ribbon：`钢筋工具区 / 按面保护层 R02`

## 核心语义

- 仅 exposed `PlanarFace`；
- stable Reference 绑定指定 Host；
- 读取 Host Rebar Cover；
- 初始中心线使用 `CoverDistance + BarModelDiameter/2`；
- 只对 `RebarPlane` handle 设置 face-specific `ToCover` preferred constraint；
- ToCover 不能确认则回滚，不留下“仅 XYZ 看起来正确”的钢筋；
- UI/MCP 共用 Service；
- MCP 默认 dryRun；
- Single / NumberWithSpacing；
- Free Form / Varying / Hook / Coupler / Lap / Collision / BS8666 不在本轮。

## 静态结果

- R00：44/44 PASS；
- R01：35/35 PASS；
- R02：44/44 PASS；
- UI：165 C# / 42 XAML / 34 Ribbon / 0 error / 0 warning；
- 全部 `tools/Test-*.py`：22 suites / 0 failure。

当前环境无 .NET/Revit，因此未 C# 编译、未证明 R02 API Runtime。

## 判定

**R02 STATIC GO / WINDOWS + REVIT 2024 RUNTIME REQUIRED / RELEASE NO-GO**
