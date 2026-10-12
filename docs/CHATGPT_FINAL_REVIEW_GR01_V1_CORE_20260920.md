# ChatGPT Independent Review — YangTools GR01 V1 Targeted Retest

Date: 2026-09-20

## Bound evidence

Result package:
`RESULT_YANGTOOLS_GR01_V1_TARGETED_MIMO_20260920_1159.zip`

Result package SHA-256:
`D613997308EA67718DF6D82F2482F33BE853594E1D7556430687E320D1265D89`

Bound source:
`YangTools_Project_3.1.38_Source_GR01_V1_STATIC_20260920.zip`

Source SHA-256:
`5126B39F901898A99FF2D9814A814816FB5EEB41C002A512A2E9C0266AB93F09`

Runtime DLL chain:
`Build = Deploy = Loaded = E6E8EC19FD45B647F393E8BDDB5C64B3AE51E7846B0996892DB9901781A38FAA`

Runtime:
- Revit 2025.4.2
- API 25.4.20.0
- PID 17196
- disposable document `ReberText`
- SOURCE_DIFF = NONE

## Targeted gate review

### 1. Multi-position control rejection — PASS

Fixture Rebar `525959` was independently read back as:

`NumberOfBarPositions = 2`

Product call rejected it with:

`CONTROL_NOT_SINGLE_POSITION`

No GR01 result set was created.

### 2. Non-straight ModelCurve rejection — PASS

Fixture `525962` was independently created/read as a `ModelArc` whose `GeometryCurve` is `Arc`, not `Line`.

Product call rejected it with:

`PATH_NOT_STRAIGHT`

No GR01 result set was created.

### 3. Edge DistributionPath — PASS

Chosen stable reference:

`19fc112c-425e-4aa8-8cc4-eab7aa518edf-0008068d:0:12:LINEAR`

Independent proof established:

1. stable representation parses;
2. owner resolves;
3. `GetGeometryObjectFromReference` returns `Edge`;
4. `Edge.AsCurve()` returns `Line`;
5. edge length = 12550.000 mm;
6. both control projections lie inside the finite edge.

GR01 Preview then returned:

- Success=true
- ChangedModel=false
- DistributionPathKind=Edge
- DistributionLength=12550 mm
- 87 bars / 86 intervals
- frozen five-zone spacing plan
- non-empty fingerprint
- Engine=ExplicitManagedFreeForm

GR01 Create then returned:

- Success=true
- ChangedModel=true
- CreatedRebarId=525976
- exactly one result Rebar Element
- PhysicalBarCount=87
- FreeFormValidationResult=Success
- MaxReadbackDeviationMm=0.0
- first/middle/last transformed geometry matches plan

A native Undo removed `525976`.

### 4. Final no-residue — PASS

Cleanup ended with only the two original control rebars:

`525944 / 525946`

Final Rebar count:

`2`

No successful GR01 result set remains.

## Consolidated GR01 V1 Core decision

Combining the previously reviewed main ModelLine runtime evidence with this targeted retest:

**GR01 V1 CORE RUNTIME GO**

Proven V1 Core scope:

- Explicit ManagedFreeForm engine
- two single-position control rebars
- ModelLine straight DistributionPath
- real model Edge straight DistributionPath
- multi-zone regularized spacing
- 12.55m / 87-bar frozen case
- Line + Arc-containing control geometry
- one Rebar Element / N physical bars
- exact physical-bar readback
- stale-fingerprint and product guard matrix
- one native Undo
- no residue

Known limits remain product scope, not current blockers:

- NativeToCover=false / ManagedGeometry
- only straight DistributionPath in V1
- V1 is Create-only; Update is V1.1
- 1000-bar performance remains NOT_TESTED
- dedicated exotic Arc scenario not separately tested
- production Bridge model was intentionally not used for the synthetic acceptance fixture

## Final labels

- `GR01 V1 CORE RUNTIME: GO`
- `GR01 V1 ENGINE: EXPLICIT_MANAGED_FREEFORM`
- `GR01 V1 MODEL_LINE PATH: GO`
- `GR01 V1 EDGE PATH: GO`
- `GR01 V1 REQUIRED SAFE-REJECTION MATRIX: GO`
- `GR01 V1 GUI: AUTHORIZED TO DEVELOP`
- `GR01 V1 GUI RUNTIME: NOT YET TESTED`
- `YANGTOOLS 3.1.38 RELEASE: NO-GO`

Next phase:

**GR01 V1 GUI — Chinese WPF interaction over the already proven shared Request/Service/Result core.**
