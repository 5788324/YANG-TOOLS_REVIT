# R09 — Rebar Collision / Clear-Spacing Screening

Date: 2026-09-18
Status: STATIC GO / RUNTIME REQUIRED

## 1. Why R09

R01–R06 closed the Shape Driven baseline; R07–R08 closed unconstrained Free Form create/modify. The next production-risk problem is not another creation button but detecting whether reinforcement physically clashes or violates the requested clear spacing.

R09 deliberately implements **Detect only**. It does not automatically move, crank, split, re-space, or rebuild reinforcement.

## 2. Geometry model

For two physical bars A/B:

`surfaceGap = minimumCenterlineDistance - (radiusA + radiusB)`

Classification:

- `surfaceGap < 0` -> `Clash`
- `0 <= surfaceGap < minimumClearance` -> `ClearanceViolation`
- otherwise -> no returned conflict

This separates a hard intersection from a clear-spacing requirement. They are not the same condition.

## 3. Supported geometry

- Shape Driven and Free Form Rebar.
- Every existing Bar Position can be expanded into a physical-bar geometry snapshot.
- Prefer `GetTransformedCenterlineCurves` so layout-position transforms are included.
- Fallback: raw centerline + `GetBarPositionTransform`.
- Straight `Line` segments: exact 3D segment-to-segment closest distance.
- Arc / other Revit curves: `Curve.Tessellate()` -> chord segments. These results are marked `TessellatedScreening` and are **not** claimed as code-compliance proof.

## 4. Performance design

R09 does not run a full Solid-vs-Solid Cartesian product.

Pipeline:

1. Expand requested Rebar elements to physical bar positions.
2. Build per-position AABB.
3. Broad-phase AABB test expanded by radii + requested clearance.
4. Only candidate pairs enter detailed centerline distance calculation.
5. Hard guards:
   - <=100 Rebar elements;
   - <=2000 physical bars;
   - <=250000 detailed candidate pairs;
   - <=1000 returned conflict records.

If the safety limit is exceeded, R09 stops and asks the caller to narrow the scope rather than freezing Revit or silently returning an incomplete PASS.

## 5. Product API

Typed service:

`RebarClearanceService.Analyze(UIApplication, RebarClearanceRequest)`

Typed MCP:

`analyze_rebar_clearance(rebar_ids, minimum_clearance_mm=25, include_same_element=false, max_results=200)`

Ribbon:

`钢筋工具区 -> 碰撞/净距 R09`

UI v1 uses selected Rebar and a fixed 25mm threshold. MCP carries the full configurable interface.

## 6. Read-only contract

R09 starts no `Transaction`, makes no model write, and returns `ChangedModel=false`.

The service must not:

- move Rebar;
- change constraints/layout/type;
- create/delete Rebar;
- create Coupler;
- create Splice;
- auto-resolve any clash.

## 7. Explicitly out of scope

- Coupler collision;
- embeds/openings/ducts/prestress collision;
- concrete cover QA;
- automatic avoidance;
- code-specific required spacing calculation;
- exact analytic Arc/Arc or Arc/Line distance (R09 uses tessellated screening for non-Line curves).

## 8. Runtime acceptance

At minimum prove on Revit 2024:

1. exact parallel Line/Line hard clash with expected numerical gap;
2. exact clear-spacing violation with expected numerical gap;
3. safe pair not returned;
4. Shape Driven + Free Form cross-engine pair supported;
5. optional same-element explicit set works with `include_same_element=true`;
6. no-residue: all inspected Rebar Id/Uid/Host/geometry unchanged before/after;
7. broad-phase candidate count is lower than the raw Cartesian pair count in a separated fixture;
8. 2024/2025 compile 0 error and 2024 Build=Deploy=Loaded SHA.
