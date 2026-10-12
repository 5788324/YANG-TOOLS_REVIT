# GR01 V1 — Explicit ManagedFreeForm Core Design

Date: 2026-09-20  
Status: STATIC IMPLEMENTED / WINDOWS + REVIT 2025 CORE RUNTIME REQUIRED / GUI NOT IMPLEMENTED / RELEASE NO-GO

## 1. Bound decision

GR01-P0 independently closed the engine decision for Revit 2025:

- Native Surface / Aligned are **not selected** for GR01 V1 because P0 did not prove a stable public API path for the required arbitrary five-zone station layout.
- GR01 V1 therefore uses **Explicit ManagedFreeForm**.
- This does not claim Autodesk native Surface/Aligned Free Form is unavailable in general.

V1 scope remains frozen to **Preview + Create + exact readback + rollback + one Undo**. Update remains V1.1.

## 2. Product inputs

`RebarGradientSetRequest`:

- `StartControlRebarId`
- `EndControlRebarId`
- optional expected control UniqueIds
- DistributionPath:
  - `ModelLine` via ElementId; or
  - model `Edge` via stable reference
- `Zones[]` using the existing `RebarGradientSpacingZoneInput`
- spacing policy parameters
- path / continuity / readback tolerances
- hard limits
- optional `ExpectedPlanFingerprint`
- `DryRun`

The two control rebars are templates only. They remain untouched and are not members of the created result.

## 3. Control-rebar guards

V1 requires:

- two different ordinary `Rebar` elements;
- exactly one physical bar position each;
- same valid Host;
- same RebarBarType;
- same RebarStyle;
- Line/Arc transformed-centerline topology that can be paired;
- no Hook;
- no End Treatment;
- no Coupler.

Unsupported termination data is rejected before any write rather than silently dropped.

## 4. DistributionPath semantics

V1 only accepts a **straight** path:

- ModelLine / non-view-specific CurveElement whose GeometryCurve is Line;
- geometry Edge whose `AsCurve()` is Line.

The path determines Station only; it does not determine bar orientation.

For each control bar, GR01 calculates a length-weighted geometry center from the transformed centerline and projects it onto the path line. The effective distribution length is the absolute projection-distance difference.

Zone order is always StartControl -> EndControl. A reversed picked path is represented by `PathReversed=true`; the Zone list is not reversed.

## 5. Geometry matching and interpolation

The service reads actual-position centerlines, preferring `GetTransformedCenterlineCurves` reflectively for compatibility.

Supported V1 curve kinds:

- Line
- Arc

It compares forward and fully reversed End-control topology and uses the smaller representative-point distance score.

For station `S`:

```text
t = S / DistributionLength
```

Line:

```text
Start(t) = Start0 + t(Start1-Start0)
End(t)   = End0   + t(End1-End0)
```

Arc:

- Start / Mid / End are interpolated independently;
- a new `Arc.Create(start,end,mid)` is validated;
- radius/center are **not** claimed to vary linearly;
- degenerate / near-collinear arcs fail the whole plan.

No Arc is silently converted to Line.

## 6. Zone / spacing

V1 reuses the P0-proven `RebarGradientSpacingPlanner`.

Frozen 12.55m example:

```text
Z1: 100 x 10
Z2: 195 x 21 + 155 tail
Z3: 100 x 20
Z4: 180 x 23 + 160 tail
Z5: 100 x 10
```

Result:

```text
86 intervals
87 bars
```

Station boundaries are merged using the existing 0.5mm default merge tolerance.

## 7. Plan fingerprint / stale-preview protection

Preview returns SHA-256 `PlanFingerprint` bound to:

- control UniqueIds;
- transformed control geometry;
- Host / BarType / RebarStyle;
- path identity and geometry;
- Zone configuration;
- spacing-policy inputs;
- calculated stations.

Create always recomputes the plan from the current model. If the UI/MCP supplies `ExpectedPlanFingerprint`, stale Preview is rejected.

Immediately before `CreateFreeForm`, the service recomputes the fingerprint again inside the same Revit execution call. A mismatch blocks the write.

## 8. Create semantics

GR01 V1 performs one explicit call:

```text
Rebar.CreateFreeForm(doc, barType, host, IList<IList<Curve>>, out validation)
```

Expected result:

```text
1 Rebar Element
N physical bar positions
```

The 2025 overload has no RebarStyle argument, so after create the service writes `RebarFreeFormAccessor.RebarStyle` from the control rebar and verifies it before commit.

The created result is intentionally:

```text
EngineUsed = ExplicitManagedFreeForm
ConstraintMode = ManagedGeometry
NativeToCover = false
```

## 9. Pre-commit invariants

Before commit GR01 validates:

- result is Free Form;
- Host == control Host;
- BarType == control BarType;
- `NumberOfBarPositions == planned bar count`;
- `IsUnconstrained == true`;
- no valid server;
- RebarStyle matches controls;
- every physical bar exists;
- every bar position transformed centerline matches the planned geometry within requested tolerance;
- control Host / BarType / geometry remain unchanged.

Any failure while transaction is Started triggers rollback.

## 10. Logging

`DiagnosticLog.WriteProductOperation` states:

- `REJECTED`
- `DRYRUN_VALIDATED`
- `COMMITTED`
- `FAILED_ROLLEDBACK`

The log carries OperationId, loaded DLL SHA, document identity, bar count, fingerprint and create validation/readback summary.

## 11. Typed MCP

New product tools:

- `preview_gradient_rebar_set`
- `create_gradient_rebar_set`

`create_gradient_rebar_set` defaults to `dry_run=true`.

GUI is deliberately deferred until the Core runtime gate is closed.

## 12. Explicit non-goals in this candidate

- no WPF / Ribbon button yet;
- no Update;
- no automatic control-bar creation/deletion;
- no curved DistributionPath;
- no Spline/NURBS controls;
- no native ToCover claim;
- no collision resolve;
- no 2027 adapter implementation.

## 13. Next runtime gate

Revit 2025 must prove with the typed product path:

1. Line-only control fixture, ModelLine path, frozen five-zone 87-bar set;
2. dryRun returns correct plan and creates nothing;
3. Apply creates exactly one Rebar Element with 87 physical bars;
4. first/last and representative middle geometry close against the plan;
5. one native Undo deletes the complete created set and leaves controls unchanged;
6. Edge path gives an equivalent valid case;
7. at least one Arc-containing control fixture closes if practical;
8. error/rollback matrix leaves no residue;
9. 200 / 500 performance smoke where practical;
10. Schedule / Mark / Numbering observations are recorded, not assumed.

Until that evidence exists:

**GR01 V1 STATIC IMPLEMENTED / REVIT 2025 CORE RUNTIME REQUIRED / GUI NOT IMPLEMENTED / RELEASE NO-GO**
