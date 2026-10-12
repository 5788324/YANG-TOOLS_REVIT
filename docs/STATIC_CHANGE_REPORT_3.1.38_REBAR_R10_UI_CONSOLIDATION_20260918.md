# Static Change Report — Rebar R10 UI Consolidation

Date: 2026-09-18

## Scope

This change is UI/Ribbon consolidation only. R10 Coupler core service/MCP semantics are not changed.

## Changes

- Re-enabled `RibbonButtonAttribute.GroupName` -> `PulldownButton` support in `RibbonBuilder`.
- Added explicit Ribbon `Order` field for stable workflow ordering.
- Consolidated Rebar Ribbon from individual large buttons into four top-level groups:
  - `创建钢筋`
  - `修改钢筋`
  - `端部与连接`
  - `检查钢筋`
- Removed R00-R10 stage labels from Rebar Ribbon/user-facing command titles.
- Moved `钢筋诊断` into `钢筋工具区 -> 检查钢筋`.
- R05 GUI now exposes only the Runtime-closed Edge -> ToCover rebind lane; Start/End exact-face rebind remains in the research/service surface and is not presented as production-ready GUI capability.
- Added static Ribbon grouping gates to `Test-UiPanelsStatic.py`.
- Updated Rebar static UI checks to the product-facing names.

## Static verification

- R00 44/44
- R01 35/35
- R02 44/44
- R03 53/53
- R04 50/50
- R05 57/57
- R06 48/48
- R07 29/29
- R08 34/34
- R09 37/37
- R10 50/50
- UI: 192 C# / 42 XAML / 43 Ribbon commands / 0 error / 0 warning
- Full `tools/Test-*.py`: 30 suites / 0 failure
- `python -m py_compile McpServer/server.py`: PASS

## Runtime boundary

No Windows/Revit build or runtime claim is made by this sandbox. The candidate must receive a fresh Windows build after its new source SHA is bound.

Status: **STATIC GO / WINDOWS BUILD + REVIT RUNTIME REQUIRED / RELEASE NO-GO**
