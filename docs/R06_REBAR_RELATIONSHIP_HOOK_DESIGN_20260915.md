# R06 — Shape Driven Relationship / Hook Design

Date: 2026-09-15
Status: STATIC GO candidate; Windows/Revit runtime required

## Purpose

R06 adds the first steel-to-steel relationship layer after R05 host-face constraint rebind.

Two minimal capabilities are included:

1. **Shape Driven Rebar -> Shape Driven Rebar `ToOtherRebar` relationship** using only native Revit constraint candidates.
2. **Start/End Hook Type modification** on an existing Shape Driven Rebar using native `SetHookTypeId`.

R06 does not attempt automatic collision resolution, automatic bridge reinforcement design, Free Form relationships, coupler creation, splice creation, or BS8666 validation.

## Why this stage exists

Autodesk documents Shape Driven constraints as capable of targeting not only host faces but also other reinforcement. In particular, Standard style bars can constrain to Stirrup/Tie handle planes and, for straight bars perpendicular to a stirrup plane, to stirrup bend/hook locations. The API exposes `RebarConstraintType.ToOtherRebar` and target metadata such as `TargetRebarConstraintType`, target edge/bend/hook end and distance.

R06 therefore tests the user's earlier practical observation that longitudinal/standard bars and stirrups are a relationship graph, not only independent XYZ curves.

## R00.5 inspector

R00.5 extends each constraint with:

- `TargetRebarElementIds`
- `TargetRebarUniqueIds`
- `TargetRebarConstraintType`
- `TargetRebarEdgeNumber`
- `TargetRebarBendNumber`
- `TargetRebarHookBarEnd`
- `DistanceToTargetRebarMm`

It also exposes `StartHookTypeId` / `EndHookTypeId` in addition to hook type names.

## ToOtherRebar service

Typed service: `RebarRelationshipService.ConstrainToOtherRebar`

Inputs:

- source Rebar ID / expected UID
- target Rebar ID / expected UID
- source handle type
- optional source handle ordinal
- target kind selector (`Any`, `Edge`, `BarBend`, `HookBend`; `Any` can expose other native target kinds)
- optional target index
- optional distance
- dry-run default true

Rules:

- source and target must be different Shape Driven Rebars;
- only Revit-provided `ToOtherRebar` candidates are accepted;
- ambiguous source handles are rejected unless an ordinal is supplied;
- ambiguous candidates are rejected unless selector fields reduce them to one;
- apply must preserve source ElementId / UniqueId / Host;
- R00.5 readback must show `ToOtherRebar` and the exact requested target Rebar;
- failure rolls back the Revit transaction.

The service intentionally does not fabricate a relationship when Revit exposes no candidate.

## Hook service

Typed service: `RebarHookModifyService.Execute`

- start and/or end Hook Type ID may be set;
- `-1` means remove hook;
- `null` means leave end unchanged;
- invalid RebarHookType or incompatible shape/hook combination is rejected by preflight/Revit and rolled back;
- ElementId / UniqueId / Host must remain unchanged;
- R00.5 readback must match requested hook IDs.

Hook orientation/rotation is out of R06 scope; that can be a later extension after basic Hook Type behavior is proven.

## MCP tools

- `list_rebar_hook_types()`
- `constrain_rebar_to_rebar(...)`
- `modify_rebar_hooks(...)`

All write tools default to `dry_run=true`.

## UI

Two minimal Ribbon commands are added under `钢筋工具区`:

- `钢筋关系 R06`
- `Hook修改 R06`

The UI calls the same typed services as MCP.

## Compatibility

R06 uses APIs present in the Shape Driven constraint system for the 2024/2025 primary test targets. It avoids creating Free Form constraints. R06 keeps version-specific preferred-constraint setting behind `RebarApiCompatibility`.

R05's missing `YangTools.Revit.Core` import is integrated into the R06 baseline.

## Gate

R06 can only become runtime GO after real Revit evidence proves:

- a real Standard/longitudinal bar receives a native `ToOtherRebar` constraint to a real Stirrup/Tie target;
- R00.5 reads the exact target Rebar and target kind/index;
- identity/host are preserved and Undo restores the prior relationship;
- at least one compatible Hook Type can be set or removed with exact readback and Undo;
- failed/ambiguous requests leave the model unchanged.
