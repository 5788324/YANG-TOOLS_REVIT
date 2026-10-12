# Rebar Runtime Roadmap Update — 2026-09-18

The original R00 planning document remains a design baseline, but its Rxx numbering was superseded by evidence-driven implementation gates.

Actual closed runtime baseline:

- R01: Shape Driven minimal create
- R02: Host Face + Cover + ToCover
- R03: surface distribution / varying / slope
- R04: Shape Driven in-place parameter modify
- R05: exact Face constraint rebind (Edge closed; Start/End remains open)
- R06: ToOtherRebar relationship + Hook
- R07: unconstrained Free Form minimal create
- R08: unconstrained Free Form in-place SetCurves modify

Closed after the original snapshot:

- R09: Level-1 Rebar↔Rebar collision / clear-spacing screening — straight/core Runtime GO; curved tessellated runtime remains NOT_TESTED.

Current:

- R10: native RebarCoupler create/readback + end-connection safety — STATIC GO, Windows/Revit runtime required.

Planned after R10 runtime evidence:

- R11: Revit 2025+ native Splice/Lap capability adapter; Revit <=2024 must not emulate native Splice by silently changing geometry
- R12: collision suggestions / limited rule-based resolution only after detection is trusted
- then production workflows (surface/path placement orchestration, starter bars, exclusion zones), propagation, BS8666/BBS and drawing-model QA.

Constrained/server-driven Free Form is not scheduled immediately. It requires a separate `IRebarUpdateServer` lifecycle and should only be opened when a production case cannot be covered by Shape Driven or raw Free Form safely.
