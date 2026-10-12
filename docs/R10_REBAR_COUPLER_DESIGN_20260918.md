# R10 — Rebar Coupler Native Minimal Closure

Date: 2026-09-18  
Status: STATIC GO / WINDOWS + REVIT RUNTIME REQUIRED / RELEASE NO-GO

## 1. Scope

R10 adds the first mechanical rebar-connection capability to YangTools. It deliberately stays separate from R11 native Splice/Lap.

Supported R10 modes:

1. `TwoBar` — connect two existing `Autodesk.Revit.DB.Structure.Rebar` elements at explicitly selected ends.
2. `SingleEndCap` — place a native Revit Coupler on one explicitly selected rebar end.

R10 does **not** copy/overlap bars and does not emulate a coupler with parameters or geometry. The apply path must call Revit native `RebarCoupler.Create`.

## 2. Autodesk API basis

Revit 2024 Developer Guide documents that:

- `RebarCoupler.Create` can connect two adjacent rebars;
- passing the second reinforcement data as `null` creates a coupler/cap on one rebar end;
- `RebarCouplerError` reports native failures such as bars not touching, different layouts, or incompatible diameter;
- `CouplerLinkTwoBars`, `Rebar.GetCouplerId`, `GetPointsForPlacement`, and `GetCouplerQuantity` provide native readback;
- Couplers are not for Area/Path/Rebar Container reinforcement.

The same core Coupler API exists well before Revit 2021, so R10 keeps the implementation in the common business layer rather than adding version-year branches.

## 3. Product surface

Shared service:

`RebarCouplerService`

Typed MCP:

- `list_rebar_coupler_types()`
- `create_rebar_coupler(...)`

Ribbon:

`钢筋工具区 -> Coupler R10`

GUI and MCP both call the same `RebarCouplerService` / request / result models.

## 4. Request contract

`RebarCouplerRequest` carries:

- optional `OperationId`;
- optional `CouplerTypeId` (null => project default, then first available fallback);
- `FirstRebarId`, `FirstEnd` (0/1), optional `ExpectedFirstUniqueId`;
- optional `SecondRebarId`, `SecondEnd` (0/1), optional `ExpectedSecondUniqueId`;
- `DryRun`, default true.

`SecondRebarId == null` selects `SingleEndCap`; otherwise it selects `TwoBar`.

## 5. Compatibility preflight

R10 dryRun is intentionally conservative and **does not start a Transaction**.

It checks before native creation:

- both target elements are ordinary `Rebar`;
- requested ends are exactly 0/1;
- optional UniqueId optimistic-lock values still match;
- TwoBar cannot target the same Rebar element twice;
- selected ends do not already contain a Coupler;
- selected ends do not contain a Hook (R10 will not silently remove Hook);
- two Rebar sets have the same `NumberOfBarPositions` before native layout validation;
- requested/default Coupler type is an `OST_Coupler` `FamilySymbol`;
- when readable, Coupler `COUPLER_MAIN_BAR_SIZE` / `COUPLER_COUPLED_BAR_SIZE` are checked against the rebar nominal diameter (0.2 mm tolerance).

`list_rebar_coupler_types` also exposes, where available:

- Bar Size 1 / 2;
- Bar Engagement 1 / 2;
- End Treatment 1 / 2 ids;
- whether the type is the project default.

DryRun does not create a temporary coupler merely to obtain native validation. Therefore geometry contact and any remaining native compatibility conditions are reported as pending until Apply.

## 6. Apply path and transaction contract

Apply uses exactly one ordinary Revit `Transaction`:

1. re-resolve Rebar/type identities;
2. create `RebarReinforcementData(firstId, firstEnd)`;
3. optionally create `RebarReinforcementData(secondId, secondEnd)`;
4. call `RebarCoupler.Create`;
5. require `RebarCouplerError.ValidationSuccessfuly` and a non-null Coupler;
6. regenerate;
7. perform exact native readback and identity invariants;
8. commit only if all invariants pass;
9. otherwise RollBack.

This gives one Ctrl+Z for a successful R10 operation.

## 7. Required readback invariants

Before commit R10 verifies:

- created Coupler type equals requested type;
- `CouplerLinkTwoBars()` matches requested mode;
- `FirstRebar.GetCouplerId(firstEnd)` equals the created Coupler id;
- in TwoBar mode, `SecondRebar.GetCouplerId(secondEnd)` equals the same id;
- `GetCoupledReinforcementData()` contains the exact requested `RebarId + End` relation(s);
- First/Second Rebar `UniqueId` and `HostId` remain unchanged;
- Coupler quantity and placement-point counts are returned for runtime evidence.

R00 Inspector already exposes `StartCouplerId` / `EndCouplerId`, so R10 before/after evidence can be independently cross-checked by the existing inspection path.

## 8. Logging

R10 emits product operation logs through `DiagnosticLog.WriteProductOperation`:

- `REJECTED`
- `DRYRUN_VALIDATED`
- `COMMITTED`
- `FAILED_ROLLEDBACK`

The log includes OperationId, loaded assembly SHA, document identity, mode, selected ends, Coupler id/native result when available, and transaction state.

## 9. Explicitly out of scope

- Revit 2025+ native Splice/Lap (R11);
- lap-length design or code checking;
- automatic selection of the correct bar end;
- automatically moving bars together when they do not touch;
- automatic generation/loading of a missing Coupler family;
- editing Coupler family geometry or end-treatment definitions;
- Area Reinforcement / Path Reinforcement / RebarInSystem / Fabric;
- coupler collision/clearance resolution.

## 10. Runtime acceptance gate

Revit 2024 must prove through the typed product path:

1. type listing returns an actual project Coupler type and its compatibility metadata;
2. TwoBar dryRun makes no model change;
3. TwoBar Apply creates a native Coupler and exact Rebar/end readback closes;
4. one native Undo restores pre-operation state;
5. SingleEndCap Apply/readback/Undo closes;
6. invalid identity/end/existing-coupler/hook/non-touching or incompatible cases fail safely with no residue;
7. source/build/deploy/loaded identity is bound to the evidence package.

Until that exists, R10 remains **STATIC GO / RUNTIME REQUIRED**, not Runtime GO and not Release GO.
