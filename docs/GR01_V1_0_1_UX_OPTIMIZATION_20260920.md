# GR01 V1.0.1 UX Optimization — ZAxis + Auto Remaining Zone

Date: 2026-09-20  
Scope: usability optimization only; no new reinforcement geometry algorithm.

## 1. Goals

1. Default distribution mode becomes **Global Z Axis** so bridge-pier workflows need only Start/End control Rebar picks.
2. Distribution length is computed automatically from the two transformed control geometries along global Z.
3. Zone table starts with one **Normal / Auto Remaining** row filling the entire effective length.
4. Adding fixed Normal/Dense zones automatically deducts from the single Auto Remaining zone.
5. Add **Swap Start/End** without repicking controls.

## 2. ZAxis semantics

`DistributionPath.Kind = ZAxis` requires no ElementId or StableReference.

Core computes the same representative geometry centers already used by GR01 and uses:

```text
minZ = min(StartCenter.Z, EndCenter.Z)
maxZ = max(StartCenter.Z, EndCenter.Z)
DistributionLength = maxZ - minZ
Axis = XYZ.BasisZ
```

`PathReversed=true` when End lies below Start on global +Z.

ZAxis defines **station direction only**. It does not rotate the bar geometry. Bar geometry/orientation still comes from Start/End control interpolation.

## 3. Auto Remaining zone

`RebarGradientSpacingZoneInput` adds:

```text
AutoLength: bool
```

Before calling the unchanged `RebarGradientSpacingPlanner`, `RebarGradientSetService` normalizes zones:

```text
remaining = DistributionLength - sum(FixedZone.Length)
AutoZone.Length = remaining
```

Rules:

- maximum one AutoLength zone;
- fixed zones still require positive length;
- fixed total > path -> `AUTO_ZONE_OVER_ALLOCATED`;
- remaining < 1mm -> `AUTO_ZONE_REMAINDER_TOO_SHORT`;
- planner receives only resolved positive lengths;
- `RebarGradientSpacingPlanner` itself is unchanged.

## 4. Fingerprint stability

The Preview request may carry `AutoLength=true, LengthMm=0`, while the GUI displays the resolved length after Preview. Create then sends that displayed length.

To prevent a false stale-preview rejection, fingerprint uses the **resolved spacing-plan length** for AutoLength zones instead of the raw UI LengthMm.

The path geometry, controls, stations, fixed-zone lengths, spacing settings and other existing fingerprint inputs remain bound.

## 5. GUI behavior

- Path choices: `全局 Z 轴 / 模型线 / 模型 Edge`.
- Default: `全局 Z 轴`.
- ZAxis disables `拾取路径`.
- Initial zone: `普通`, `AutoLength=true`, MaxSpacing=200mm.
- New Normal: fixed 1000mm / 200mm.
- New Dense: fixed 1000mm / 100mm.
- Auto row's Length cell cannot be edited directly.
- Checking another row as Auto automatically clears the previous Auto row.
- Footer shows path length, fixed allocation and auto remainder when known.
- `⇅ 交换首末` swaps Id/UniqueId/display and invalidates Preview while preserving zone order from the new Start side.

## 6. MCP parity

Example:

```json
{
  "distribution_path": {"kind":"ZAxis"},
  "zones": [
    {"label":"加密", "length_mm":1000, "max_spacing_mm":100},
    {"label":"普通", "length_mm":4250, "max_spacing_mm":200},
    {"label":"加密", "length_mm":2000, "max_spacing_mm":100},
    {"label":"普通", "auto_length":true, "max_spacing_mm":200},
    {"label":"加密", "length_mm":1000, "max_spacing_mm":100}
  ]
}
```

With an effective Z length of 12550mm, Auto Remaining resolves to 4300mm and preserves the frozen 87-bar plan.

## 7. Unchanged scope

- Explicit ManagedFreeForm;
- ManagedGeometry / NativeToCover=false;
- one Rebar Element + N physical bars;
- Line/Arc control interpolation;
- Create-only V1;
- ModelLine and Edge remain supported;
- no curved DistributionPath;
- no V1.1 Update;
- no 2027 adapter work.
