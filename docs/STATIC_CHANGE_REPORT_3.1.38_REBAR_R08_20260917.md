# Static Change Report — Rebar R08

- Added `RebarFreeFormModifyModels` / `RebarFreeFormModifyService`.
- Added Ribbon command `Free Form修改 R08`.
- Added typed MCP command/tool `modify_free_form_rebar`.
- MCP defaults `dry_run=true`.
- R08 uses native `RebarFreeFormAccessor.SetCurves` and only accepts unconstrained/no-server Free Form Rebar.
- Explicitly rejects Shape Driven and server-driven/constrained Free Form.
- Does not call `DisconnectFromServer`.
- Preserves ElementId/UniqueId/Host/FreeForm engine and validates requested bar count before commit.
- Supports explicit Free Form set bar-count changes (e.g. 3 -> 4 paths) without Shape Driven layout semantics.
- Updated UI static expected Ribbon count to 41 and cumulative Rebar tests.
- New `Test-RebarR08`: 34/34 PASS.
- R00–R08 focused static suites all PASS.
- UI static: 0 error / 0 warning / 41 Ribbon commands.
- Full `tools/Test-*.py`: 28 suites / 0 failure.
- MCP Python compile: PASS.

Status: **R08 STATIC GO / WINDOWS REVIT RUNTIME REQUIRED / RELEASE NO-GO**.
