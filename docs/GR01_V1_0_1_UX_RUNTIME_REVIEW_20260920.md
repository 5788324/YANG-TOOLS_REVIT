# GR01 V1.0.1 UX Runtime Review — 2026-09-20

## Verdict

- ZAxis Preview / AutoLength / Swap / Over-allocation guards: PASS on Revit 2025.4.
- ZAxis Create: NOT CLOSED. Current fixture returned `RebarCantBeBent`.
- GUI interactive ZAxis UX: NOT TESTED in this pass.
- YangTools release: NO-GO.

## SOURCE_DIFF adjudication

Mimo found one real product-wiring omission in `RevitExecutionBroker.ExecuteGradientRebarSet`: `autoLength` was not mapped into `RebarGradientSpacingZoneInput` for `preview_gradient_rebar_set` / `create_gradient_rebar_set`.

Accepted fix:

```csharp
AutoLength = zone["autoLength"]?.Value<bool?>() ?? false,
```

This patch only aligns MCP request mapping with the already implemented GUI/Core AutoLength model. It does not change spacing, interpolation, transaction, or CreateFreeForm algorithms.

The static contract was strengthened so the broker must contain the AutoLength mapping in both the pure spacing preview path and the GR01 set execution path. This would have caught the original omission.

## RebarCantBeBent analysis

The returned native enum is not evidence that the 138mm wall is too thin. Autodesk documents `RebarCantBeBent` as a FreeForm validation failure when the input bar cannot be bent according to the bar type bending radius.

The runtime fixture also used two U-shaped controls whose curves lie in the XZ plane and themselves span about 600mm in Z, while ZAxis also distributes the set along Z. This is not the intended bridge-pier transverse-bar fixture (normally the control bar plane should be approximately transverse to Z). Therefore the current evidence cannot distinguish:

1. fixture/orientation incompatibility for a FreeForm set;
2. an intermediate reconstructed bend falling on a native bend-radius tolerance boundary;
3. another native FreeForm set validation constraint.

No production geometry algorithm is changed from this evidence alone.

## Next gate

Run a targeted Revit 2025 Create test using a purpose-built ZAxis fixture:

- same valid Rebar Host;
- two single-position control bars separated ~12550mm in global Z;
- control bars are transverse/horizontal to the Z distribution direction (XY or equivalent plane), not long U bars whose main segments run along Z;
- each control bar independently exists successfully with the selected BarType;
- first test one Auto Normal zone; then Dense 1000 + Auto Remaining;
- Create must produce one Rebar Element / expected physical bars and one native Undo must remove it.

If a correctly transverse fixture still returns `RebarCantBeBent`, stop and investigate per-bar native validation / bend radius before changing the GR01 algorithm.
