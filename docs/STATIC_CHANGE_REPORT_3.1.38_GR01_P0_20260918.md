# Static Change Report — GR01-P0 Gradient Rebar Capability + Spacing Foundation

Date: 2026-09-18

## Baseline

Input source:

`YangTools_Project_3.1.38_Source_REBAR_R10_UI_CONSOLIDATED_STATIC_20260918.zip`

Baseline SHA-256 is recorded in the parent R10 UI consolidation report. This GR01 candidate must receive a new SHA and must not reuse the R10 runtime identity.

## Scope

This change starts GR01 development without expanding V1 requirements.

Implemented only:

1. **GR01-P0 Revit native capability probe** (read-only):
   - discovers `Rebar.CreateFreeForm` overloads at runtime;
   - discovers Aligned / Surface built-in server GUID API surface;
   - reads RebarUpdateService registered server ids;
   - probes `RebarFreeFormAccessor` methods, including whether `SetLayoutAsCustomSpacing` actually exists in the loaded version;
   - does not claim Native Surface/Aligned usability from API presence alone.
2. **GR01-P0 server skeleton create probe**:
   - optional Surface / Aligned `CreateFreeForm(serverGUID)` minimal attempt;
   - always executes inside a temporary Transaction and forcibly RollBack;
   - final `ChangedModel=false`;
   - explicitly does not configure Surface/Aligned handles and therefore is not a Native Engine PASS.
3. **GR01 pure spacing planner foundation**:
   - Zone length / max spacing validation;
   - regularized spacing search with default 5mm step and 0.75 minimum tail ratio;
   - manual actual-spacing override;
   - short-zone rule;
   - finite search / explicit `REGULAR_SPACING_NOT_FOUND` failure;
   - Zone boundary station generation;
   - 0.5mm Station de-duplication;
   - bar-count hard limit;
   - no Revit model write.
4. Typed MCP tools:
   - `probe_gradient_rebar_capabilities`
   - `preview_gradient_spacing_plan`
   - `probe_gradient_server_create`
5. Added GR01 v0.3 requirement document and P0 static test.

## Explicitly not implemented

- No GR01 Ribbon / WPF yet.
- No control-Rebar selection / geometry interpolation yet.
- No production GR01 create service yet.
- No Update.
- No 2027-specific adapter.
- No claim that Native Surface / Aligned works for GR01.

## Static verification

- `Test-RebarGR01P0.py`: **43/43 PASS**.
- All `tools/Test-*.py`: **31 suites / 0 failure**.
- Existing UI remains **43 Ribbon commands**; GR01 does not add another large Ribbon button during P0.
- `python -m py_compile McpServer/server.py`: **PASS**.
- C# Build: **NOT RUN** in the current Linux sandbox because dotnet/MSBuild/Revit SDK are unavailable.

## Frozen math fixture

For:

`1.00 + 4.25 + 2.00 + 4.30 + 1.00 = 12.55m`

with Zone max spacing `100 / 200 / 100 / 200 / 100 mm`, static reference planning yields:

- `100 x 10`
- `195 x 21 + 155`
- `100 x 20`
- `180 x 23 + 160`
- `100 x 10`

Total: `86 intervals / 87 bars`.

## Status

**GR01-P0 STATIC FOUNDATION GO / REVIT 2025 BUILD + NATIVE CAPABILITY POC REQUIRED / GR01 V1 NOT YET IMPLEMENTED / RELEASE NO-GO**
