# YangTools 3.1.38 静态变更报告

## 合并范围

### PasteCAD Shared
- `ImportPlacement.Shared`
- Shared 失败不 fallback Origin
- placement diagnostics

### Family Management V2.1
- `Core/StableParameterKey.cs`
- `Core/FamilyUsageService.cs`
- Family Manager usage/unused/safe cleanup/jump/multi-RFA
- Instance Manager scope/filter/status/batch type/batch parameter
- UI size contracts

### Batch Publisher V2
- `Core/BatchTasks/BatchDocumentOpener.cs`
- `Core/BatchTasks/BatchExportNaming.cs`
- `Core/BatchTasks/BatchTaskEngine.cs`
- `Models/BatchTask/BatchTaskViewModel.cs`
- `UI/BatchTaskWindow.xaml(.cs)`
- `tools/Test-BatchPublisherV2.py`

关键变化：target-document resource resolution、native cloud metadata、NWC/IFC/DWG/PDF/Print 独立 preflight、exact naming、whole-batch path planning、staging/safe replace、2025 PDF background off。

## UI

- Family Manager：1280×780 / Min1080×650 / Row30 Header36
- Family Instance：1360×800 / Min1160×660 / Row30 Header36
- Batch Publisher：1320×860 / Min1120×720 / target grid Row30 Header34 / left panel 360
- 所有关键表格/筛选/路径控件有固定或最小宽度；窗口缩小依赖横向滚动，不允许单字列/按钮叠加。

## 静态结果

- `Test-BatchPublisherV2.py`: 99/99 PASS
- `Test-BatchPrintPdf.py`: 32/32 PASS
- `Test-FamilyManagementV2.py`: 110/110 PASS
- `Test-PasteCad.py`: 19/19 PASS
- `Test-CadToRevit.py`: 76/76 PASS
- all `tools/Test-*.py`: 17/17 suites PASS
- UI: 151 C# / 42 XAML / 37 roots / 4 themes / 31 Ribbon / 0 error / 0 warning
- C# lexical/braces: 151/151 PASS

本环境没有 Windows dotnet/MSBuild/Revit，不能提供当前 DLL build/runtime 证据。

结论：`STATIC GO / WINDOWS REVIT UNIFIED RETEST REQUIRED / RELEASE NO-GO`。
