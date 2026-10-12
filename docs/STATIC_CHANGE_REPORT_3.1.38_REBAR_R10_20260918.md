# Static Change Report — Rebar R10 Coupler

Date: 2026-09-18

## Baseline

Input source:

`YangTools_Project_3.1.38_Source_REBAR_R09_STATIC_20260918.zip`

Baseline SHA-256:

`3000A6D9CC64EF6EFFBAC3FA2C5A80B9BA920DBDA3300569EB39F040DF5E0AB1`

The baseline SHA was independently recalculated from the Drive ZIP before R10 development.

## Changes

- Added `RebarCouplerModels.cs`.
- Added `RebarCouplerService.cs`.
- Added native `TwoBar` and `SingleEndCap` modes using `RebarCoupler.Create`.
- Added Coupler type discovery with project-default marker and Bar Size / Engagement / End Treatment metadata.
- Added dryRun preflight for Rebar identity, end 0/1, occupied Coupler, Hook, set-position count, type category and readable bar-size compatibility.
- Added exact post-create readback through `GetCouplerId`, `CouplerLinkTwoBars`, `GetCoupledReinforcementData`, quantity and placement points.
- Added `UniqueId` / `HostId` preservation gate before commit.
- Added one-Transaction apply / rollback semantics and product operation logging.
- Added typed MCP tools:
  - `list_rebar_coupler_types`
  - `create_rebar_coupler`
- Added Ribbon command `Coupler R10` using the same service.
- Added `Test-RebarR10.py`.
- Updated cumulative Ribbon expectation from 42 to 43 in current static suites.

## Static verification

- `Test-RebarR10`: **50/50 PASS**.
- R00–R10 focused suites: **44 / 35 / 44 / 53 / 50 / 57 / 48 / 29 / 34 / 37 / 48 — all PASS**.
- UI static: **192 C# / 42 XAML / 43 Ribbon / 0 error / 0 warning**.
- Full `tools/Test-*.py`: **30 suites / 0 failure**.
- `python -m py_compile McpServer/server.py`: **PASS**.
- Local C# build: **NOT RUN** because this Linux sandbox does not contain `dotnet`/MSBuild/Revit SDK; Windows Fresh Build remains a required acceptance gate.

## Scope hygiene

R10 does not implement Splice/Lap and does not modify Rebar geometry to force compatibility. Geometry/contact/layout conditions that cannot be proven without native creation are left to `RebarCoupler.Create` / `RebarCouplerError` during Apply.

## Status

**R10 STATIC GO / WINDOWS + REVIT 2024 RUNTIME REQUIRED / RELEASE NO-GO**
