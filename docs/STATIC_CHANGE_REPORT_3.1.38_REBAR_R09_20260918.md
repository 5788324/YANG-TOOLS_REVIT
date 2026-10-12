# Static Change Report — Rebar R09

- R08 E-only runtime retest closes all six safe-rejection paths; R08 final status is **CORE RUNTIME GO / CONSTRAINED FREE FORM NOT_TESTED / RELEASE NO-GO**.
- Added `RebarClearanceModels` and read-only `RebarClearanceService`.
- Added typed MCP tool `analyze_rebar_clearance`.
- Added Ribbon command `碰撞/净距 R09`.
- R09 distinguishes hard clash from requested clear spacing by centerline distance and bar radii.
- Shape Driven and Free Form physical bar positions are supported.
- Prefers transformed centerlines; supports bar-position-transform fallback.
- Straight lines use exact 3D segment distance; non-Line curves use tessellated screening and are explicitly marked approximate.
- Added AABB broad-phase and hard workload limits to protect Revit UI responsiveness.
- R09 is read-only: no Transaction and `ChangedModel=false`.
- No Coupler/Splice creation and no automatic avoidance in R09.
- `Test-RebarR09`: **37/37 PASS**.
- R00–R09 focused static suites all PASS: 44/35/44/53/50/57/48/29/34/37.
- UI static: **189 C# / 42 XAML / 42 Ribbon / 0 error / 0 warning**.
- Full `tools/Test-*.py`: **29 suites / 0 failure** (executed in batches because the combined shell loop exceeds the sandbox command timeout).
- MCP Python compile: PASS.

Status: **R09 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**.
