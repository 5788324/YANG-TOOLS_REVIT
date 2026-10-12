# Static Change Report — YangTools 3.1.38 GR01 V1.0.1 UX

Date: 2026-09-20

## Changed

- `RebarGradientDistributionPathInput.Kind` default: `ZAxis`.
- Core `ResolveDistributionPath` adds global-Z mode using control geometry centers.
- `RebarGradientSpacingZoneInput` adds `AutoLength`.
- `RebarGradientSetService` resolves one AutoLength zone before the unchanged spacing planner.
- Fingerprint uses resolved AutoLength length to keep Preview/Create stable.
- MCP supports `ZAxis` and `auto_length`.
- WPF defaults to ZAxis + one Normal Auto Remaining zone.
- WPF adds Start/End swap, auto-row toggle/readonly length, live fixed/remainder summary, ZAxis path-pick disable.
- Existing ModelLine/Edge, Explicit CreateFreeForm, transaction/readback/rollback logic is retained.

## Static verification

- `Test-RebarGR01V1.py`: 81/81 PASS
- `Test-RebarGR01V1Gui.py`: 44/44 PASS
- `Test-RebarGR01V101UX.py`: 41/41 PASS
- all `tools/Test-*.py`: 34 suites / 0 failure
- UI static: 201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning
- `McpServer/server.py` py_compile: PASS

## Not run here

- Windows C# Fresh Build: NOT_RUN (no dotnet/MSBuild in current environment)
- Revit 2025 Runtime for new ZAxis/AutoLength branch: NOT_TESTED

## Verdict

`GR01 V1.0.1 UX STATIC GO / WINDOWS BUILD + REVIT 2025 TARGETED RUNTIME REQUIRED / RELEASE NO-GO`
