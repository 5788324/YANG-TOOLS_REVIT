# YangTools 3.1.38｜批处理与云链接 Batch Publisher V2

## 1. 用户目标

一次维护多个本地/云 Revit 模型，对每个模型可靠执行：

- NWC
- IFC
- DWG
- 原生 PDF Export（Revit 2022+）
- PDF Print（Revit 2021+）

要求：跨模型设置可预检、输出命名可预测、失败不破坏旧成果、云模型语义真实、每个模型失败原因可追溯。

## 2. 为什么重做

旧版把当前活动文档里的 View/SheetSet/Export Setup 名称和对象当作整个批次的事实源；多模型配置不一致时会在执行中途失败。DWG/PDF 文件名也受 Revit Export 规则影响，导致 UI 的“文件名”并不一定等于实际文件名。

V2 的原则是：**任务配置保存名称/意图，每打开一个目标 Document 后重新解析真实资源，并在写任何文件前完成预检。**

## 3. 模型来源

- local/network RVT
- Desktop Connector 同步 RVT
- already-open Document
- already-open native cloud Document

Cloud item 保存：Region + ProjectGuid + ModelGuid。需要重新打开时构建 Cloud ModelPath。V2 不做 APS/ACC project tree browser。

外部 workshared RVT 仅为导出 detached open，任务结束无保存关闭；当前已经打开的 Document 不由 YangTools 关闭。

## 4. 预检

每个模型执行前检查：

- Document 能打开/读取；
- 输出目录；
- 所选 view/sheet set 存在且可用；
- 所选 DWG/PDF/Print setup 在**这个 Document**中存在；
- NWC/IFC/DWG optional functionality；
- printable view requirements；
- 命名模板可以解析；
- 全批次所有最终 primary path 不冲突；
- Overwrite=false 的已有文件；
- 目标文件路径不能已是目录。

预检失败的模型为 0 输出。

## 5. 命名

Tokens：

- `{模型名}`
- `{模型文件名}`
- `{图纸号}`
- `{图纸名}`
- `{视图名}`
- `{序号}`
- `{日期}`

默认：

- NWC `{模型名}.nwc`
- IFC `{模型名}.ifc`
- DWG `{模型名}-{图纸号}-{图纸名}.dwg`
- PDF `{模型名}-{图纸号}-{图纸名}.pdf`

非法文件名字符统一安全替换。多视图同模板若仍产生重复路径，通过稳定序号去重。

## 6. NWC

范围：WholeModel / Auto3D / Current3D / Named3D。
配置：Coordinates、ExportLinks、ConvertElementProperties、ExportElementIds、ExportRoomAsAttribute、ExportRoomGeometry、DivideFileIntoLevels。

NWC Exporter 不存在时返回 NOT_INSTALLED；不允许用其它格式假冒。

## 7. IFC

范围：WholeModel / Auto3D / Named3D。
配置：IFCVersion、ExportBaseQuantities、SpaceBoundaryLevel、WallAndColumnSplitting。

IFC Export 在 Transaction 内执行；功能不可用时 preflight 阻止。

## 8. DWG

范围：AllSheets / AllPrintableViews / CurrentView / NamedViewSheetSet。

目标模型内按名称读取 ExportDWGSettings；可选择遵守 setup 坐标，或覆盖 Shared/Project；可设置 MergedViews。

为精确主文件名，每个 View/Sheet 独立调用 Export。输出到 staging folder，必须出现预期 `baseName.dwg` 且非空；该次导出产生的其它文件/XRef 一起 commit。

## 9. 原生 PDF Export

只编译于 Revit 2022+。

- Combined：一次 Export，多 View，一个指定文件名；
- Separate：逐 View 一次 Export，仍令 `Combine=true`，由 YangTools 指定最终 FileName；不依赖 Revit `Combine=false` 的 NamingRule。
- Revit 2025+：强制 `SetExportInBackground(false)`。

每个 expected PDF 必须实际存在且非空。

## 10. PDF Print

Revit 2021+。流程：

1. snapshot PrintManager state；
2. SelectNewPrintDriver；
3. PrintRange.Select；
4. InSession ViewSet；
5. PrintSetting；
6. CombinedFile + PrintToFile + PrintToFileName；
7. SubmitPrint；
8. 等待实际 PDF；
9. safe replace；
10. restore user print state。

某些 PDF driver 会弹 Save As，出现这种情况就不是无人值守 PASS。

## 11. 输出安全

NWC/IFC/DWG：写 staging，验证成功，再提交；如果覆盖旧输出，先 backup，commit 成功才删除 backup，失败尽力恢复。

PDF/Print：写临时文件，验证后 `ReplaceOutputSafely`。

全批 `batchOutputReservations` 禁止本次新文件互相覆盖；OverwriteExisting 仅允许替换任务开始前已有的旧文件。

## 12. UI

窗口 1320×860，Min1120×720。
模型 DataGrid 高190，Row30/Header34；左侧链接区360，右侧输出设置区自适应；发布类型为 NWC/IFC/DWG/PDF Export/PDF Print 五个 Tab。

跨模型命名资源使用 editable ComboBox；用户可以输入公司统一 setup 名，即使当前活动模型不包含该名称，最终由逐模型 preflight 判断。

## 13. 验收

必须真实生成文件并记录 expected/actual path、bytes、SHA；不接受直接 Python/Revit API 手工导出替代 YangTools 产品链。
