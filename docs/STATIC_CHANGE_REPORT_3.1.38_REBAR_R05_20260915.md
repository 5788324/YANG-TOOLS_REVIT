# Static Change Report — YangTools Rebar R05 — 2026-09-15

## Scope

R05 adds M2 constraint retargeting for existing Shape Driven Rebar.

### New
- `RebarConstraintRebindModels.cs`
- `RebarConstraintRebindService.cs`
- `RebarConstraintRebindCommand.cs`
- `tools/Test-RebarR05.py`
- MCP tool `rebind_shape_rebar_constraints`

### Updated
- R00 inspector -> schema R00.4 with exact target host-face stable references
- `RebarPlanarFaceSelectionFilter` -> optional required host guard
- MCP broker registration
- cumulative Ribbon expectation -> 37
- previous Rebar tests updated to cumulative R00.4 / Ribbon 37 expectations

## Static results

- R05: 57/57 PASS
- all `tools/Test-*.py`: 25 suites / 0 failures
- UI static: 175 C# / 42 XAML / 37 Ribbon / 0 error / 0 warning

## Runtime status

No .NET/Revit runtime is available in the current environment.
Status: **R05 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**.
