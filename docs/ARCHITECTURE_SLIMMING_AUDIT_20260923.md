# YangTools 3.1.38 Architecture Slimming Audit

Date: 2026-09-23

## Executive conclusion

YangTools is not excessively large by raw code volume, but it is structurally dense. The main problem is not "too many features"; it is that mature, experimental, UI, Revit API, MCP, build/release, tests, and historical evidence have accumulated in the same source tree and several high-complexity files.

Current measured baseline:
- about 47,965 C# lines across ~201 C# files;
- 43 XAML files and 44 Ribbon commands;
- UI code-behind ~16,319 C# lines;
- Core ~22,013 C# lines;
- 10 C# files >=800 lines, 7 >=1000 lines;
- 12 WPF code-behind files directly reference Revit Transaction APIs;
- 18 WPF code-behind files directly use FilteredElementCollector;
- `RevitExecutionBroker.cs` is ~1,146 lines and centrally dispatches MCP operations;
- `Commands/` and `UI/` are largely flat folders;
- `docs/` contains a large volume of historical acceptance/change material;
- generated `artifacts/verification` evidence was being carried inside source packages even though `artifacts/` is gitignored.

This is maintainable today but scales poorly if new features continue to be added in the same pattern.

## Target architecture

Use a feature-sliced modular monolith, not many DLL projects.

```text
src/YangTools.Revit/
  Features/
    Rebar/
      GradientSet/
      TestTools/
    Family/
    Batch/
    CadToRevit/
    Project/
    Text/
  Shared/
    Revit/
    UI/
    Infrastructure/
  Mcp/
    Infrastructure/
    Handlers/
```

Each production feature should converge on:

`Command/WPF/MCP adapter -> Request -> Service -> Result`

Only Service/compatibility layers should own product Revit write logic. UI and MCP must not duplicate transactions or geometry algorithms.

## Migration priorities

### P0 — Release/tooling hygiene (now)
- repair PowerShell interpolation defects;
- guarantee correct Unicode installer filenames in canonical source packaging;
- add one source-package command;
- stop carrying generated `artifacts/verification` history in source packages;
- add one static-test runner;
- do not touch accepted Rebar business algorithms.

### P1 — MCP decomposition
`RevitExecutionBroker.cs` should become ExternalEvent/queue infrastructure only. Domain dispatch should be split into handlers (Rebar, document/query, Python). Preserve command names and wire contracts.

### P2 — UI business-logic extraction
Start with the largest/highest-coupling windows. Extract Revit reads/writes and transactions into services without redesigning UI. Candidate order:
1. ProjectAssetManagerWindow
2. FamilyInstanceManagerWindow
3. FamilyManagerWindow
4. ChineseCheckWindow
5. SheetManagerWindow

### P3 — Feature folders
Move files by vertical feature after their service boundary is clean. Keep namespaces stable during the first move to reduce runtime risk.

### P4 — Docs/tests organization
- active design docs in `docs/active/` or indexed from one active-doc index;
- historical acceptance belongs in Google Drive / `docs/archive`, not the working root;
- static tests eventually move from mixed `tools/` into `tests/static/` with compatibility runner.

## Do not do

- no full rewrite;
- no split into many assemblies now;
- no MVVM rewrite of every existing window at once;
- no renaming MCP commands during structural migration;
- no reimplementation of already Runtime-GO Rebar algorithms;
- no deletion of experimental Rebar services merely because they are hidden under `钢筋测试`.

## Acceptance principle

Every structural batch must be behavior-preserving and small enough to bind to a source SHA. Static tests first; Windows build next; runtime smoke only for surfaces touched by that batch.
