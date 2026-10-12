# YangTools Rebar R05 Mimo Result Review

Date: 2026-09-15
Reviewer: ChatGPT
Input result zip SHA-256: `C82176D356FCF7097FE51DBEBD511D46729E71F6F014FA48F60682AD6D41B983`

## Final status

**R05 EDGE REBIND CORE RUNTIME GO**  
**START/END REBIND OPEN**  
**UI_INTERACTIVE NOT_TESTED**  
**RELEASE NO-GO**

## Accepted evidence

- Source zip SHA matches `9CB3F6519FE4EBA046F9F4B1DD3B9D7B62DBDC421B5D0C5FF13F4137BF0358C7`.
- One compile-only fix was required: add `using YangTools.Revit.Core;` to `RebarPlanarFaceSelectionFilter.cs`; this is integrated into R06 baseline.
- Revit 2024 and 2025 compile with 0 errors in Mimo evidence.
- Revit 2024 Build/Deploy/Loaded DLL SHA all equal `6485F27E619F8DD63D72815DD7BFA99A2E1C42ACC752BFF66D5DD9399DABB6C0`.
- R00.4 can read exact host-face stable references.
- Edge rebind on Rebar 321999 changes exact target face from `...:9:SURFACE` to `...:10:SURFACE` while preserving ElementId, UniqueId, Host and Varying mode.
- Undo restores exact Edge target back to `...:9:SURFACE`.
- Error matrix rejects invalid ID/UID/ref, no-op and empty request without leaving the changed target.

## Open issue: Start/End

Start/End was not proven. In the supplied fixture the left/right faces returned no ToCover candidate. A bottom-face Start dry-run found a candidate but real apply failed exact readback and rolled back. Therefore R05 must not be described as full Edge/Start/End runtime GO.

This does not block R06 because R06 uses the proven M2 concept on a separate native relationship type (`ToOtherRebar`) and does not depend on Start/End host-face retargeting.

## Screenshot discrepancy

The file named `screenshots/revit2024_r05.png` visually shows **Autodesk Revit 2025.4 - Bridge 1 RC.rvt**, not Revit 2024. The screenshot is rejected as 2024 UI evidence. Runtime 2024 is still supported by journal/MCP/loaded-path evidence. UI remains NOT_TESTED.

## Carry-forward

- Keep a backlog item to build a dedicated Start/End fixture and re-test exact host-face rebind later.
- Do not reopen Edge rebind unless regression evidence appears.
