# Static Change Report — YangTools 3.1.38 Rebar R06

Date: 2026-09-15
Status: **STATIC GO / RUNTIME REQUIRED / RELEASE NO-GO**

## Changes

- Integrated R05 minimal compile fix (`using YangTools.Revit.Core;`).
- R00 inspector schema bumped to **R00.5**.
- R00.5 reads exact ToOtherRebar target Rebar ID/UID, target kind, edge/bend/hook index and target distance.
- R00.5 reads Start/End Hook Type IDs.
- Added `RebarRelationshipService` with native ToOtherRebar candidate discovery, ambiguity guards, identity/host invariants, exact readback and rollback.
- Added `RebarHookModifyService` with dry-run, native SetHookTypeId, identity/host readback and rollback.
- Added MCP tools `list_rebar_hook_types`, `constrain_rebar_to_rebar`, `modify_rebar_hooks`.
- Added Ribbon commands `钢筋关系 R06` and `Hook修改 R06`.
- Ribbon count advanced to **39**.

## Static results

- R00: 44/44 PASS
- R01: 35/35 PASS
- R02: 44/44 PASS
- R03: 53/53 PASS
- R04: 50/50 PASS
- R05: 57/57 PASS
- R06: **48/48 PASS**
- All `tools/Test-*.py`: PASS
- UI static: **180 C# / 42 XAML / 39 Ribbon / 0 error / 0 warning**
- `python -m py_compile McpServer/server.py`: PASS

Static/source tests are not Revit runtime proof. No C# compiler/Revit runtime is available in the ChatGPT container.

## Deferred

- R05 Start/End exact face rebind remains open.
- Free Form relationship engine.
- Hook orientation/rotation.
- Coupler/splice/lap.
- Collision/clear-spacing engine.
- BS8666/BBS.
