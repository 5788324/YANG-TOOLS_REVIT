# Mimo Local Execution Task — Rebar R09 Collision / Clearance

Date: 2026-09-18
Final GO/NO-GO owner: ChatGPT

## 0. Hard rules

- Work only from the bound R09 source ZIP and verify its sidecar SHA first.
- Do not use Google Drive.
- Do not touch Revit 2022.
- Revit 2024: build + deploy + runtime.
- Revit 2025: compile-only unless specifically needed; do not open the user's active 2025 work.
- Do not modify unrelated YangTools code.
- If a compile failure is caused solely by a new R09 Revit API signature, only a minimal signature/namespace compatibility fix is allowed; attach exact diff and mark `SOURCE_DIFF`. Otherwise `NO_SOURCE_DIFF`.
- Final status in your report remains `EXECUTION CONDITIONAL`; ChatGPT decides GO/NO-GO.

## 1. Identity gate

Record:

- source ZIP SHA-256;
- Revit 2024 fresh build log: errors/warnings;
- Revit 2025 compile-only log;
- Build DLL SHA;
- deployed DLL SHA;
- loaded assembly absolute path + SHA from the running Revit 2024 process;
- PID, Revit version, document.

Required: 2024 `Build = Deploy = Loaded`.

## 2. Static gate

Run all `tools/Test-*.py` suites. Expected current totals:

- R00 44
- R01 35
- R02 44
- R03 53
- R04 50
- R05 57
- R06 48
- R07 29
- R08 34
- R09 37
- UI: 42 Ribbon / 0 error / 0 warning
- all 29 Test suites: 0 failure
- `python -m py_compile McpServer/server.py`

## 3. Controlled fixture

Use a valid Rebar host and one known RebarBarType. Fixture construction itself may use direct API/Python, but the feature under test **must** be called through the typed R09 product command `analyze_rebar_clearance`.

Prefer straight, parallel physical bars with the same diameter so the expected gap is easy to calculate. Record actual model diameter from product/Inspector output rather than assuming a nominal value.

Create enough bars to test the following. Keep their straight centerlines parallel and overlapping in length.

### A — hard clash

Pick A/B with known centerline separation `dAB` such that:

`dAB < radiusA + radiusB`

Call R09 with `minimum_clearance_mm=25`.

Required:

- `Status=Clash`;
- `GeometryAccuracy=ExactLineSegments`;
- returned `CenterlineDistanceMm` approximately equals known `dAB`;
- returned `SurfaceGapMm` approximately equals `dAB - radiusA - radiusB` (tolerance <=0.5mm).

### B — clear-spacing violation, but no clash

Pick A/C such that:

`radiusA + radiusC < dAC < radiusA + radiusC + 25mm`

Required:

- `Status=ClearanceViolation`;
- `0 <= SurfaceGapMm < 25`;
- numerical gap tolerance <=0.5mm.

### C — safe pair

Pick A/D such that surface gap >25mm.

Required:

- no conflict record for A/D;
- no false `Clash` / `ClearanceViolation`.

### D — cross-engine

Use one Shape Driven straight bar and one Free Form straight bar at a known distance below threshold.

Required: R09 returns the correct pair and gap; it must not reject either engine.

### E — same Rebar set

Create an explicit Free Form set with at least two spatially separated bar positions at a known spacing.

1. call with `include_same_element=false`: no self-set pair should be checked/returned;
2. call with `include_same_element=true`: the two positions should be analyzed and classified according to the known gap.

### F — broad-phase / no residue

Create or use several bars far outside the clearance threshold.

Required:

- `BroadPhaseCandidatePairs` is less than the raw all-pairs count for the fixture;
- feature stays responsive;
- before/after Rebar count identical;
- inspect all fixture Rebars before/after and prove Id/Uid/Host/centerline geometry unchanged;
- `ChangedModel=false`.

## 4. Optional curved screening

If a bent/arc Rebar sample is already readily available, run one case and confirm the returned conflict marks `GeometryAccuracy=TessellatedScreening`. Do not fabricate a PASS if no suitable sample exists: mark `CURVED_SCREENING_NOT_TESTED`.

## 5. Error matrix

Use valid geometry where applicable so the intended guard is reached:

- empty `rebar_ids` at MCP layer;
- one Rebar with `include_same_element=false`;
- invalid RebarId;
- `minimum_clearance_mm < 0`;
- `minimum_clearance_mm > 5000`;
- `max_results=0` at product layer if invoking raw gateway;
- >100 RebarIds if practical via repeated invalid IDs is not valid because IDs dedupe/validate; this limit may remain static-only if a real 101-Rebar fixture is not practical.

Every failed case must leave the model unchanged.

## 6. Evidence package

Return one ZIP:

`RESULT_YANGTOOLS_REBAR_R09_MIMO_YYYYMMDD_HHMM.zip`

Include:

- `RESULT.md`
- `sha/`
- `build/2024.log`, `build/2025.log`
- `static/`
- `mcp_raw/` request+response for A–F/error cases
- `rebar_json/` before/after Inspector output
- `fixture/` description including coordinates, bar type ID/diameter and expected gap math
- `journal/` Revit 2024 excerpt
- `screenshots/` only if a real Revit 2024 window is captured
- `diff/` only when SOURCE_DIFF exists

Do not repeat R08 testing and do not start Coupler/Lap/Splice or automatic clash resolution.
