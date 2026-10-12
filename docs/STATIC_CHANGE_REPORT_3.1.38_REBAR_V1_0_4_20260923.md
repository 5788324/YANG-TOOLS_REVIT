# Static Change Report — YangTools 3.1.38 / Rebar V1.0.4

Date: 2026-09-23
Parent source: `YangTools_Project_3.1.38_Source_REBAR_RIBBON_V1_0_3_STATIC_20260922.zip`
Parent SHA-256: `E6EAD29308876EF5BD0B8FFDF247AC4276AD08B9242EF505BA21A4CF689452D7`

## Product changes

1. GR01 auto spacing changed from 5mm-step + 0.75-tail search to `UniformWithinMax`.
2. `REGULAR_SPACING_NOT_FOUND` removed from automatic planning.
3. Manual actual spacing remains explicit and may produce a final tail.
4. Legacy `SpacingStepMm/MinTailRatio` wire fields retained but ignored by auto planning.
5. Plan fingerprint now contains `UNIFORM_WITHIN_MAX` and excludes ignored legacy fields.
6. GUI footer explains the new default policy.
7. MCP docstrings explain new semantics.
8. Rebar Ribbon V1.0.3 production/test split and icons are carried forward unchanged and still require first Revit smoke.

## Static results

- `Test-RebarGR01V104Spacing.py`: 31/31 PASS
- `Test-RebarGR01P0.py`: 43/43 PASS
- `Test-RebarGR01V1.py`: 81/81 PASS
- `Test-RebarGR01V101UX.py`: 41/41 PASS
- `Test-RebarGR01V102UI.py`: 40/40 PASS
- `Test-RebarGR01V1Gui.py`: 44/44 PASS
- `Test-RebarRibbonV103.py`: 87/87 PASS
- all `tools/Test-*.py`: 37 suites / 0 failure
- MCP `py_compile`: PASS

C# Fresh Build / Revit Runtime: NOT_RUN in ChatGPT environment.

## Required Windows/Revit gate

One combined Mimo run must bind the new source SHA and cover:

- V1.0.3 Ribbon/icon smoke;
- new 29130.046 / Max100 and Max200 planner results;
- five-zone 12.55m new 85-bar plan;
- one Create/readback/native Undo under new station plan;
- GUI MaxSpacing=100 Preview no longer failing.
