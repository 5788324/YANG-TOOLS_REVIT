# 2026-09-11 Reliability Candidate Compatibility Note

本轮 F01 文件服务、F02/F04 Batch 请求/窗口、F03 事务状态、F05 日志均需重新按年份编译。主测 Revit 2024；net48 年份与 net8+/net10 年份必须独立 Build，不复用 DLL。

当前 Linux 环境无 C# compiler，因此以下状态均不能凭静态检查提升：
- 2021：BUILD REQUIRED；Print PDF Runtime 仍 NOT_TESTED；
- 2022/2023：BUILD REQUIRED；
- 2024：PRIMARY BUILD + RUNTIME REQUIRED；
- 2025：BUILD + relevant Runtime REQUIRED；
- 2026：未安装则 NOT_INSTALLED；
- 2027：缺 .NET10 则 NOT_BUILT。

`DiagnosticLog` 使用 `Document.IsModelInCloud`，Windows 年份构建必须同时验证 API 兼容。

# YangTools 3.1.38｜Revit 2021–2027 兼容性

## 当前状态

| Revit | Target | PDF Export | Print PDF | 3.1.38 当前状态 |
|---|---|---|---|---|
| 2021 | net48 | 不支持 | 支持 | BUILD/RUNTIME RETEST REQUIRED |
| 2022 | net48 | 支持 | 支持 | BUILD REQUIRED |
| 2023 | net48 | 支持 | 支持 | BUILD REQUIRED |
| 2024 | net48 | 支持 | 支持 | PRIMARY RUNTIME RETEST REQUIRED |
| 2025 | net8.0-windows | 支持 + background API | 支持 | BUILD + RUNTIME RETEST REQUIRED |
| 2026 | net8.0-windows | 支持 | 支持 | BUILD REQUIRED；未安装则 NOT_INSTALLED |
| 2027 | net10.0-windows | 支持 | 支持 | 缺 .NET10 时 NOT_BUILT |

本表不把 NuGet 编译写成 Runtime PASS。

## Batch Publisher V2 年份差异

### Revit 2021
- 编译时不包含原生 PDF Export 路径；
- PDF 仅使用 PrintManager；
- 需要真实 PDF printer；驱动若弹交互窗口不能无人值守。

### Revit 2022–2024
- 原生 PDF Export；
- separate mode 采用 one-view Combine=true 以获得 deterministic FileName；
- PrintManager 仍可作为独立选项。

### Revit 2025+
- 原生 PDF 支持后台导出；Batch Publisher 明确 `SetExportInBackground(false)`，保证当前批处理同步等待输出。

### NWC / IFC / DWG
- 每个宿主先由 OptionalFunctionalityUtils 判断实际能力；
- NWC Exporter 未安装必须 NOT_INSTALLED；
- IFC/DWG 功能不可用必须在 preflight 阶段阻止。

### Cloud Model
- 保存 Region/Project GUID/Model GUID；
- 用 `ConvertCloudGUIDsToCloudPath` 构建 ModelPath；
- 只对当前会话已打开的 native cloud model 建立任务项，不假设存在 ACC Browser API。

## Family/PasteCAD

Family V2 使用成熟 API：FilteredElementCollector / FamilySymbol / FamilyInstance / ChangeTypeId / Parameter identity / Transaction/SubTransaction / Trial Delete。

PasteCAD Shared 使用 `ImportPlacement.Shared`；实机坐标对齐必须单独证明。

## Runtime 门禁

2024 为主功能验收；2021 专门做 Print PDF；2025 至少启动一次验证 PDF background-off 和 UI/最小写入路径。2026/2027 未安装不能写 Runtime PASS。

## Authenticode 与年份 DLL

每个 Revit 年份的 `YangTools.Revit.dll` 都是独立编译产物，签名也必须逐 DLL 执行。签名会改变 DLL SHA，因此验收记录必须区分：

- `Build SHA`：签名前编译产物；
- `Signed Release SHA`：Authenticode 签名后产物；
- `Deploy SHA / Loaded SHA`：正式部署时必须与 `Signed Release SHA` 一致。

不能继续用“Build == Deploy”作为签名发布版唯一门禁；签名版正确关系是：`Signed Release == Deploy == Loaded`。
