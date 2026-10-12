# ChatGPT Independent Review — YangTools GR01-P0

Date: 2026-09-20

## Bound evidence

Result package:
`RESULT_YANGTOOLS_GR01_P0_MIMO_20260920_0932.zip`

Result package SHA-256:
`B3D0534A634E38016F918C283B041FEB47266C6B140D8EE99519E080F00EBBE0`

Original bound source SHA-256:
`7BEA6D65856CF07D8BFF4593827C9B4745A8342C753463E2FBC3DACD578E8AB8`

Runtime source state:
`SOURCE_DIFF` — only the GR01 probe namespace alias fix described in `diff/Rebar_namespace_alias.patch`.

## Identity

- Revit runtime: 2025.4.2
- Revit API runtime: 25.4.20.0
- PID: 18912
- disposable document: `ReberText`
- production Bridge model was not used
- Build / Deploy / Loaded DLL SHA:
  `C472393583BEE2E1E5EC2E53D11ADCB3A31E675C0C64A9627DD97339A5D8BE74`
- Revit 2025 build: 0 error / 308 warnings
- Revit 2024 compile-only: 0 error / 272 warnings

## Static

- GR01-P0: 43/43 PASS
- all `tools/Test-*.py`: 31 suites / 0 failure
- UI: 43 Ribbon commands / 0 static UI error / 0 warning
- MCP py_compile: PASS

## Independent evidence review

### A — Revit 2025 capability

Confirmed at runtime:

- server-guid `Rebar.CreateFreeForm(Document, Guid, RebarBarType, Element)` overload exists;
- explicit-curves `CreateFreeForm` overloads exist;
- `RebarUpdateService` exists;
- two Autodesk RebarUpdate servers are registered:
  - `FreeFormRebar.LongitudinalReinfServer` / `表面分布服务器`
  - `FreeFormRebar.RebarTransversalServer` / `横向钢筋服务器`
- public/static `FreeFormAlignedServerGuid` was not exposed by this runtime;
- public/static `FreeFormSurfaceServerGuid` was not exposed by this runtime;
- `HasValidAlignedServer()` exists;
- `HasValidSurfaceServer()` was not exposed by this runtime;
- `SetLayoutAsCustomSpacing` is not present in this Revit 2025 runtime;
- 2027-style 3D Path computation API is not present.

Important limitation:
the presence of Autodesk internal registered servers proves that Revit has native Free Form distribution infrastructure, but P0 did not prove a stable public 2025 API contract that YangTools can configure and use for GR01's arbitrary five-zone layout. Therefore those built-in servers are not selected as the GR01 V1 engine.

### B — frozen spacing planner

The 12.55m frozen example closed exactly:

- Z1: 100 × 10
- Z2: 195 × 21 + 155
- Z3: 100 × 20
- Z4: 180 × 23 + 160
- Z5: 100 × 10
- 86 intervals
- 87 bars
- all intervals <= zone maximum
- ChangedModel=false

Error guards also closed for zone-length mismatch, max-spacing <= 0, manual-spacing > max, and bar hard limit.

### C — native server skeleton

Surface and Aligned probes did not create a Rebar because the tested public/static server GUID route was unavailable.

Rebar count remained 0 -> 0, proving no residue.

This is **not** evidence that Revit's own UI cannot create Surface/Aligned Free Form. It is evidence that the tested YangTools public-API route was not sufficient for GR01 V1.

## SOURCE_DIFF adjudication

The only runtime source modification was the namespace/type disambiguation:

`using RevitRebar = Autodesk.Revit.DB.Structure.Rebar`

in:

- `RebarGradientCapabilityService.cs`
- `RebarGradientServerCreateProbeService.cs`

This is a compile-only namespace collision fix (`YangTools.Revit.Core.Rebar` namespace versus Autodesk `Rebar` type). It does not change the spacing algorithm, engine decision semantics, transaction behavior, or model-writing behavior.

The fix has been integrated into the accepted development baseline and the full local static suite has been rerun successfully.

## Final P0 decision

**GR01-P0 ENGINE DECISION GO**

For **Revit 2025 GR01 V1**, select:

**`Explicit ManagedFreeForm`**

as the production development engine.

Native Surface / Aligned are retained only as future adapters / research paths and are **not dependencies of V1**.

This decision does not mean Autodesk native Surface/Aligned Free Form is generally unavailable. It means P0 did not prove a sufficiently stable public 2025 API route for GR01's required single-set + arbitrary Zone stations + deterministic readback.

## Status labels

- `GR01-P0 EVIDENCE REVIEW: PASS`
- `GR01-P0 ENGINE DECISION: GO`
- `REVIT 2025 V1 ENGINE: EXPLICIT_MANAGED_FREEFORM`
- `NATIVE_SURFACE: NOT_SELECTED / NOT_PROVEN FOR GR01 V1`
- `NATIVE_ALIGNED: NOT_SELECTED / NOT_PROVEN FOR GR01 V1`
- `NATIVE_NONUNIFORM_ZONE: NOT_PROVEN`
- `GR01 V1: DEVELOPMENT AUTHORIZED`
- `GR01 V1 RUNTIME: NOT YET TESTED`
- `YANGTOOLS 3.1.38 RELEASE: NO-GO`
