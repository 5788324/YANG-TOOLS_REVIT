# R05 Shape Driven Constraint Rebind Design — 2026-09-15

## Goal

R05 is the first M2 stage: retarget existing Shape Driven Rebar constraints without deleting and rebuilding the Rebar.

R05 intentionally does not change host, topology, Free Form geometry, hooks/couplers/laps, or collision routing.

## Supported retargets

For an existing Shape Driven Rebar on the same valid Rebar Host:

- `Edge -> exposed host PlanarFace -> ToCover`
- `StartOfBar -> exposed host PlanarFace -> ToCover`
- `EndOfBar -> exposed host PlanarFace -> ToCover`

One or more targets may be requested through MCP. The UI intentionally retargets one handle per action.

## Required invariants

After retargeting:

- same ElementId
- same UniqueId
- same Host
- same Varying/Uniform mode
- every unrequested constraint remains unchanged
- every requested handle is `ToCover`
- every requested handle resolves to the exact requested host face

If any invariant fails, the Transaction is rolled back.

## R00.4 readback

R05 upgrades the inspector to R00.4.

Each host-face constraint now includes `TargetHostFaceStableReferences`, read from
`RebarConstraint.GetTargetHostFaceReference(...)` and converted with
`Reference.ConvertToStableRepresentation(doc)`.

This is required because `TargetSummary=Wall#123` is insufficient to distinguish different faces on the same host.

## No-op protection

R05 rejects a requested face that is already the current target face. A no-op must not be reported as a successful model change.

## Version compatibility

Preferred-constraint writing remains centralized in `RebarApiCompatibility`:

- Revit 2021–2024: `SetPreferredConstraintForHandle`
- Revit 2025+: `SetPreferredConstraint`

## Out of scope

- changing Rebar host
- curved-face retargeting
- multiple ambiguous handles of the same requested HandleType
- Free Form
- Rebar-to-Rebar retargeting
- hooks / couplers / laps
- collision solve
- M3 rebuild
