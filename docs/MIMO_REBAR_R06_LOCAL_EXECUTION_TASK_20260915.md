# Mimo Local Execution Task — YangTools Rebar R06

Date: 2026-09-15
Final GO/NO-GO authority: ChatGPT
Mimo role: Windows / Revit executor only

## 0. Input binding

Use only the R06 source zip contained in this handoff. Verify the sidecar SHA before any work. Do not fetch from Google Drive.

Do not edit source unless compilation is impossible. If a minimal compile fix is required, make the smallest possible change and return an exact diff. Never silently alter behavior.

## 1. Build gate

Fresh build:

- Revit 2024: required, 0 errors
- Revit 2025: compile-only required, 0 errors

Record warnings. Run all `tools/Test-*.py`, with explicit R00-R06 counts and UI static result. Expected current Ribbon count: **39**.

## 2. SHA gate

For Revit 2024 record:

- build DLL SHA-256
- deployed DLL SHA-256
- SHA-256 of the exact assembly path obtained from the running YangTools assembly

Require Build = Deploy = Loaded. Include PID, Revit version and loaded path. Journal/MCP path is primary evidence; screenshot alone is not enough.

## 3. R00.5 readback gate

Confirm `inspect_rebar` returns `SchemaVersion=R00.5` and that Hook IDs and ToOtherRebar fields are present.

## 4. Fixture rules

Create a small disposable structural/rebar fixture in Revit 2024. Fixture creation may use temporary Revit Python because it is test setup, not the product action.

Required relationship fixture:

- one **Standard style straight/longitudinal bar** (source);
- one **Stirrup/Tie style** Shape Driven rebar (target), placed so Revit naturally offers at least one `ToOtherRebar` candidate to the source bar;
- record both ElementIds, UniqueIds, host IDs, shapes/styles and geometry.

If the first fixture exposes no candidate, adjust geometry/orientation and try at least two reasonable configurations. If no native candidate can be produced, report `RELATION_NOT_TESTED`, do not fake PASS.

Required Hook fixture:

- at least one Shape Driven Rebar;
- at least one RebarHookType. If the template has none, fixture setup may create a default RebarHookType.

## 5. A — relation dry-run

Call typed MCP `constrain_rebar_to_rebar(..., dry_run=true)`.

PASS requires:

- `ChangedModel=false`;
- at least one native `ToOtherRebar` candidate;
- candidate reports target Rebar ID/UID and target kind/index when available;
- model/Rebar counts unchanged.

If multiple handles/candidates exist, use returned information to specify `source_handle_ordinal`, `target_kind`, and/or `target_index`; do not alter product code merely to choose one.

## 6. B — apply exact ToOtherRebar

Apply one unique candidate using typed MCP only.

PASS requires:

- source ElementId unchanged;
- source UniqueId unchanged;
- source Host unchanged;
- R00.5 current constraint is `ToOtherRebar`;
- `TargetRebarElementIds` contains the exact target Rebar ID;
- `TargetRebarUniqueIds` contains target UID;
- target kind/index readback agrees with selected candidate where applicable;
- transaction committed.

Save raw before/after JSON.

## 7. C — relation Undo

Use real Revit Undo (`PostCommand.Undo` is acceptable if journal proves execution). After Undo, prove the new ToOtherRebar relationship is gone or the previous constraint is restored, while source Rebar still exists with same identity.

## 8. D — Hook dry-run / apply / Undo

1. `list_rebar_hook_types()` and save raw output.
2. `modify_rebar_hooks(..., dry_run=true)`; prove model unchanged.
3. Apply a compatible Start or End Hook Type with typed MCP.
4. R00.5 must read exact Hook Type ID and same Rebar ElementId/UniqueId/Host.
5. Undo and prove hook state returns to before.

If the selected bar/shape does not permit the Hook Type, try at least one other reasonable Shape Driven fixture. If no compatible sample can be made without redesigning product code, report `HOOK_NOT_TESTED` with raw Revit error.

## 9. E — error matrix

At minimum verify these fail without model change:

- source == target;
- invalid source ID;
- invalid target ID;
- expected source UID mismatch;
- expected target UID mismatch;
- ambiguous handle without ordinal when multiple exist;
- ambiguous candidate without selector when multiple exist;
- invalid Hook Type ID;
- Hook request with neither end specified.

Free Form rejection may be tested only if a real sample is easily available; otherwise `NOT_TESTED`.

## 10. UI

Actually clicking `钢筋关系 R06` / `Hook修改 R06` is useful but not a core blocker. If not done, explicitly state `UI_INTERACTIVE_NOT_TESTED`.

Any screenshot claimed as Revit 2024 must visibly show a Revit 2024 title bar. Do not reuse a Revit 2025 screenshot.

## 11. Return package

Return one ZIP named:

`RESULT_YANGTOOLS_REBAR_R06_MIMO_YYYYMMDD_HHMM.zip`

Include:

- RESULT.md
- source SHA verification
- build 2024/2025 logs
- static logs/reports
- Build/Deploy/Loaded SHA evidence
- journal excerpts/full journal as practical
- fixture description and IDs
- raw MCP JSON for A-E
- before/after/undo R00.5 JSON
- source diff or `NO_SOURCE_DIFF`
- screenshots if captured

Mimo must write `EXECUTION CONDITIONAL`; final GO/NO-GO is reserved for ChatGPT.
