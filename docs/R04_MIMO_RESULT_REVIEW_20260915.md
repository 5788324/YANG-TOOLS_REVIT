# R04 Mimo Result Review — 2026-09-15

## Final verdict

**R04 CORE RUNTIME GO / UI_INTERACTIVE NOT_TESTED / RELEASE NO-GO**

## Verified evidence

- Result ZIP SHA-256: `0FA0CD559B312612873BDAE4D1BC581AF4B8CF8C818D61BD0AC9254F5BA7AC0F`
- Input source SHA: `936EC338C68F2655DA8F3943D472BF14C26576D920975D8F00475C3EA722EE8A`
- Revit 2024 build: 0 error.
- Revit 2025 compile-only build: 0 error.
- Build / Deploy / Loaded SHA (2024): `F0683AD8D4F079B23037E5BDC2A5B5470ACB48388B0B93BBD9024A1011C7C6DB`.
- R00.3 actual-position readback uses `TransformedCenterline`; uniform set sample X = 1000 / 400 / 0 mm.
- Varying set 6@200 -> 8@150 stayed same ElementId/UniqueId/Host and kept Edge/Start/End `ToCover`.
- Varying sample lengths after modify remained non-uniform.
- Bar type 12.7 -> 16 mm changed in place; identity and constraints remained.
- Revit Undo restored 7@175 back to 8@150 on the same Rebar.
- Error cases left the target unchanged.
- `diffs/README.txt` says `NO_SOURCE_DIFF`.

## Evidence correction

`screenshots/revit2024_r04.png` does **not** show Revit 2024.3. The visible title bar is **Autodesk Revit 2025.4 - Bridge 1 RC.rvt**.
Therefore this screenshot is rejected as 2024 UI evidence.

This does not invalidate the R04 core runtime verdict because the result package independently contains:
- a Revit 2024 journal (`journal.0105.txt`) whose header reports `Release: 2024.3.4` / `Autodesk Revit 2024`;
- MCP readbacks reporting `RevitVersion=2024`;
- a loaded assembly path under `...\Addins\2024\YangTools\YangTools.Revit.dll`;
- a Build=Deploy=Loaded SHA triple tied to that loaded 2024 path.

The UI button click remains **NOT_TESTED**.

## Decision

R04 proves M1 in-place Shape Driven modification sufficiently to proceed to R05 / M2 constraint retargeting.
