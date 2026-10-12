# YangTools 3.1.38｜Rebar R03 Static Change Report

日期：2026-09-15
状态：**R03 STATIC GO / Revit 2024 Runtime Required / RELEASE NO-GO**

## 输入

继承 R02 工作树，并吸收 R02 Mimo 已验证的 Revit 2024 API 修复：

`SetPreferredConstraintForHandle(handle, constraint)`。

## 核心新增

- `Core/Rebar/RebarApiCompatibility.cs`
- `Core/Rebar/RebarSurfaceVaryingModels.cs`
- `Core/Rebar/RebarSurfaceVaryingCreateService.cs`
- `Commands/RebarSurfaceVaryingCreateCommand.cs`
- `tools/Test-RebarR03.py`

## 核心修改

- R02 preferred-constraint 调用迁入 compatibility shim；
- R00 varying 状态读取改为跨版本兼容；
- MCP Broker / Python server 增加 `create_shape_rebar_varying_on_face`；
- Ribbon 增加 `钢筋工具区 / 表面变长 R03`；
- R01/R02 UI 累计 Ribbon 静态期望更新到 35。

## R03 几何策略

- Cover face、Start boundary、End boundary 必须属于同一有效 Rebar Host；
- 当前仅 exposed PlanarFace；
- `distributionNormal = barDirection × coverNormal`，让 set 沿宿主面分布；
- Edge -> Cover ToCover；
- StartOfBar -> Start boundary ToCover；
- EndOfBar -> End boundary ToCover；
- 任一约束失败，Transaction rollback；
- varying 通过版本兼容层开启。

## 静态结果

- R00：44/44 PASS
- R01：35/35 PASS
- R02：44/44 PASS
- R03：53/53 PASS
- UI：169 C# / 42 XAML / 35 Ribbon / 0 error / 0 warning
- 全部 `tools/Test-*.py`：23 suites / 0 failed

当前执行环境无 Windows/Revit/dotnet 编译门禁，不宣称 C# Build PASS。

## 已知边界

- 未验证 R03 在 Revit 2024 runtime；
- 未验证 2025+ compatibility branch 编译；
- 未验证真实 varying length；
- 未验证斜平面；
- Free Form / 曲面不在 R03；
- UI interactive 尚未验收。
