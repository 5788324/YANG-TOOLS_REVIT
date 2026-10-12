# R06 Mimo Result Review — 2026-09-17

## Final decision

R06 RELATIONSHIP + HOOK CORE RUNTIME GO / UI_INTERACTIVE NOT_TESTED / RELEASE NO-GO.

## Accepted evidence
- Source ZIP SHA: 80601F4E723008002248C2AB04453F143041B58717DBC25E9B86E6C8CC8288C0.
- Revit 2024 and 2025 build: 0 error.
- 2024 Build = Deploy = Loaded SHA: B86FECB5E0DBE8BBFC27D9BBDCB2A196420CCF230D18A1900A1B653541C1D64F.
- R00.5 fields present.
- Source Standard bar 321989, target StirrupTie 321994.
- Dry-run returned 3 native ToOtherRebar candidates.
- Exact Edge ordinal 0 -> target Edge index 0 was applied and read back as ToOtherRebar to target 321994.
- Undo restored the source Edge to FixedDistanceToHostFace while preserving source identity/host.
- Start HookType 321995 applied and Undo restored empty hook.
- Eight negative cases failed without model damage.
- Screenshot visibly shows Autodesk Revit 2024.3 / Project1.

## Important qualification
Before the explicit Edge retarget, the source RebarPlane already had a native ToOtherRebar relationship to target 321994 in the fixture. Therefore R06 proves that YangTools can discover/select/apply an exact native ToOtherRebar candidate on the source Edge and undo that edit. It does not prove that every ToOtherRebar relationship was created from a completely relation-free source.

## Open
- UI interactive product click remains NOT_TESTED.
- Free Form relation remains out of scope.
- Coupler/Lap remain out of scope.
