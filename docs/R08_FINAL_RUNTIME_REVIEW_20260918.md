# Rebar R08 Final Runtime Review — 2026-09-18

## Final verdict

**R08 FREE FORM IN-PLACE MODIFY CORE RUNTIME GO / CONSTRAINED FREE FORM NOT_TESTED / RELEASE NO-GO**

R08 的 A–D 已由完整结果包闭环，E-only 定向复测又把六类拒绝路径补齐，因此 R08 本身不再保留安全拒绝门禁。

## Identity

- Source ZIP SHA-256: `E9EA2A69796645AB6CEF81B86D726A2E7D0D394A4F94ECADCB62A5A2ACDEDB6E`
- Build = Deploy = Loaded: `D8EB6CB5237B9097CEC57F0F9EDB18992A067B8FE58FE64279647C71FEEFF0EF`
- R08 full result ZIP SHA-256: `2A3E9B2D966BAC8330CC50E708D8CF9FBB31CFFDCADC80A8AE68D3B7112AEC53`
- R08 E-only result ZIP SHA-256: `12BD79B8326D72C85DDC2B345B49F3976400E2D5DE5645E0D7DBF3329F1F9D34`
- E-only runtime: Revit 2024.3.4 / PID 21884 / Project1
- NO_SOURCE_DIFF

## Accepted runtime evidence

1. unconstrained Free Form single bar uses native `RebarFreeFormAccessor.SetCurves` in-place; ElementId/UniqueId/Host preserved and geometry changed.
2. explicit Free Form set changes 3 -> 4 bar positions on the same Rebar; native layout reads `FixedNumber`; Undo restores 4 -> 3.
3. Free Form remains `EngineMode=FreeForm`, `IsUnconstrained=true`, no valid server.
4. Dry run does not change the model.
5. E-only retest proves product-level rejection for:
   - Shape Driven target;
   - invalid RebarId;
   - wrong ExpectedUniqueId;
   - one-point bar;
   - consecutive duplicate / <1mm segment;
   - >500 bars.
6. Before/after E-only inspection is identical after normalization by ElementId; Rebar count remains 3 -> 3 and there is no successful R08 transaction.

## Deliberate boundary

`CONSTRAINED_FREEFORM_NOT_TESTED` remains valid. R08 must not call `DisconnectFromServer` to force a server-driven Free Form into the raw-curve path. Constrained/server Free Form requires a separate `IRebarUpdateServer` architecture if/when it is justified.

## Next decision

Do not expand R08 into constrained Free Form. The two engine baselines are now sufficient to move to production QA. R09 is therefore **Level-1 Rebar Collision / Clear-Spacing Screening**, before Coupler/Lap/Splice automation.
