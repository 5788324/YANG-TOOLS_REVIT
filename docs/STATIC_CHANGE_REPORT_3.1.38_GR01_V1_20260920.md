# Static Change Report — YangTools 3.1.38 GR01 V1

Date: 2026-09-20

## Baseline

Accepted GR01-P0 development baseline:

`YangTools_Project_3.1.38_Source_GR01_P0_ACCEPTED_20260920.zip`

P0 engine decision:

`Explicit ManagedFreeForm` for Revit 2025 GR01 V1.

## Implemented

Added:

- `Core/Rebar/RebarGradientSetModels.cs`
- `Core/Rebar/RebarGradientSetService.cs`
- `tools/Test-RebarGR01V1.py`

Extended:

- `Mcp/RevitExecutionBroker.cs`
- `McpServer/server.py`

Typed MCP:

- `preview_gradient_rebar_set`
- `create_gradient_rebar_set`

No Ribbon/WPF entry was added in this candidate.

## V1 behavior

- two single-position control Rebars;
- same Host / BarType / RebarStyle;
- explicit reject for Hook / End Treatment / Coupler;
- transformed centerline readback;
- Line/Arc topology matching with automatic full reversal;
- straight ModelLine or Edge DistributionPath;
- effective Station by control-geometry projection;
- existing Zone/regularized-spacing planner;
- XYZ interpolation for all bar positions;
- one `Rebar.CreateFreeForm` call -> one result Rebar Element;
- copied RebarStyle through FreeFormAccessor;
- all physical positions exact geometry readback before commit;
- controls verified unchanged;
- SHA-256 PlanFingerprint / stale-preview rejection;
- dryRun no write;
- single transaction / rollback / one-Undo semantics;
- product operation logging.

## Static verification

- `Test-RebarGR01V1.py`: **80/80 PASS**.
- Existing GR01-P0: **43/43 PASS**.
- All `tools/Test-*.py`: **32 suites / 0 failure**.
- UI static: **198 C# / 42 XAML / 43 Ribbon / 0 error / 0 warning**.
- `python -m py_compile McpServer/server.py`: **PASS**.
- Ribbon command count remains 43; GR01 GUI is not added yet.

## Environment limitation

This Linux environment does not contain dotnet/MSBuild/Revit SDK runtime, therefore:

- C# Build: **NOT RUN** here;
- Revit 2025 Runtime: **NOT RUN** here;
- Arc runtime: **NOT TESTED**;
- 200/500/1000 performance: **NOT TESTED**;
- Schedule / Mark / Numbering behavior: **NOT TESTED**.

## Status

**GR01 V1 STATIC IMPLEMENTED / WINDOWS BUILD + REVIT 2025 CORE RUNTIME REQUIRED / GUI NOT IMPLEMENTED / RELEASE NO-GO**
