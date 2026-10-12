# R07 — Free Form Minimal Create

## Goal
Open the second Rebar engine without pretending unconstrained Free Form is equivalent to Shape Driven constraints.

## Scope
- Native Rebar.CreateFreeForm based on one or more bar curve chains.
- Input coordinates are centerline points in mm.
- One path -> one Free Form bar.
- Multiple paths -> one Free Form set controlled by native Free Form semantics.
- Standard / StirrupTie style.
- dryRun by default for MCP.
- Transaction rollback and R00 inspector readback.

## Explicit boundary
The selected API creates unconstrained Free Form Rebar. Constraints cannot simply be added later. A constrained/custom Free Form path requires an IRebarUpdateServer/server GUID architecture and is deliberately NOT part of R07.

## Product API
MCP: create_free_form_rebar(host_id, bars, bar_type_id?, rebar_style?, dry_run=True)

bars example:
[
  [[0,0,0],[1000,0,200],[1800,400,500]],
  [[0,200,0],[1000,200,250],[1800,600,600]]
]

## Runtime gates
1. dryRun does not change model.
2. Create a non-planar 3+ segment Standard Free Form bar in Revit 2024.
3. Inspector must report EngineMode=FreeForm and the created ElementId/host/type correctly.
4. If accessor exposes IsUnconstrained, it must be true for this R07 path.
5. Create a 3-bar Free Form set from three explicit paths; inspect count/layout and transformed geometry.
6. Once Undo must remove the whole product transaction.
7. Bad host, empty bars, <2 points, zero-length segment, invalid BarType all fail safely.
8. Revit 2025 compile-only 0 error.

## Not in R07
- IRebarUpdateServer / constrained Free Form
- surface distribution solver
- Free Form constraint modification
- Coupler / splice / lap
- collision resolution
- BS8666 mapping
