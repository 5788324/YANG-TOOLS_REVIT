# Mimo 本地执行任务｜YangTools Rebar R02

日期：2026-09-15  
角色：Windows / Revit 2024 实机执行器  
最终验收：ChatGPT

## 0. 输入

只使用用户转发给你的本地完整交接包。不要访问 Google Drive，不要向用户索要项目背景。

固定源码：

`YangTools_Project_3.1.38_Source_REBAR_R02_STATIC_20260915.zip`

执行前必须核对同目录 SHA sidecar。全新目录解压，禁止覆盖 R00/R01 目录。

## 1. 权限

你可以：

- Fresh Build；
- 部署到 Revit 2024 当前用户 Addins；
- 启动 Revit 2024；
- 使用空白测试项目创建临时墙、RebarBarType、CoverType 等测试 fixture；
- 使用 MCP / execute_revit_python 做 fixture 和证据读取；
- 若发生**纯 API 签名编译错误**，允许最多 3 个最小修复，但必须保存 diff，并在 RESULT 顶部标为 source changed。

你不可以：

- 扩功能；
- 重构；
- 修改 R02 设计语义；
- 把 NOT_TESTED 写成 PASS；
- 宣布最终 GO；
- 动 Revit 2025 用户工作环境。

## 2. Build / 静态门禁

必须执行并保存原始输出：

- Revit 2024 build：0 errors；warnings 记录但不要求为 0；
- `Test-RebarR00.py`：44/44；
- `Test-RebarR01.py`：35/35；
- `Test-RebarR02.py`：44/44；
- `Test-UiPanelsStatic.py`：34 Ribbon / 0 error / 0 warning；
- 全部 `tools/Test-*.py` 必须 0 failure。

记录：Build DLL size + SHA-256。

## 3. Build = Deploy = Loaded

必须证明：

- Build DLL SHA；
- Deploy DLL SHA；
- 从**运行中的 Revit 2024 AppDomain/Assembly**取得 YangTools.Revit loaded path；
- 对该 loaded path 文件重新计算 SHA；
- 三者一致。

保存 PID、Revit version、document title。

## 4. Fixture

在 Revit 2024 新建/使用干净测试项目：

- 一个有效混凝土 Rebar Host（优先基础墙/结构墙）；
- 至少一个 RebarBarType；
- 宿主 exposed face 必须有明确 Cover Type，记录其原始距离；
- 需要时创建第二个 CoverType，例如比原 Cover 大 25 mm，用于回归；
- fixture 写操作不计入产品写路径，但必须单独记录 ElementId。

## 5. R02-A｜host face probe

调用：

`inspect_rebar_host_faces(host_id)`

必须保存 raw JSON，并确认至少一个 face 同时满足：

- `IsPlanar=true`；
- `IsExposed=true`；
- `StableReference` 非空；
- CoverTypeId / CoverDistanceMm 非空；
- normal 非零。

选择其中一个 face 作为后续固定输入，记录 stable reference。

## 6. R02-B｜dryRun

调用 `create_shape_rebar_on_face(..., dry_run=true)`。

要求：

- Success=true；
- ChangedModel=false；
- 返回 CoverDistance、BarModelDiameter、ExpectedCenterlineOffset；
- Rebar 数量 before == after；
- 不启动/提交产品 Transaction。

## 7. R02-C｜Single + ToCover

使用同一 face：

`create_shape_rebar_on_face(..., layout=Single, dry_run=false)`

必须：

- TransactionStatus=Committed；
- CreatedRebarId 非空；
- PreferredToCoverApplied=true；
- PreferredConstraintType=`ToCover`；
- R00.2 readback 为 ShapeDriven / Standard；
- R00 constraints 中 Rebar Plane 的 current/preferred 约束可读为 `ToCover`；
- 实际中心线距原宿主面的距离记录为 ActualCenterlineOffsetMm。

计算：

`expected = CoverDistanceMm + BarModelDiameterMm / 2`

如果 `abs(actual - expected) <= 2 mm`：PASS。  
如果不满足：**不要自行修代码**，标 PARTIAL，保存 raw JSON / 截图 / journal，交 ChatGPT 判断 Revit Cover 语义或实现是否需调整。

## 8. R02-D｜Cover 修改跟随（本轮关键）

在单独 fixture Transaction 中：

1. 给上面同一 exposed face 指派一个新的 RebarCoverType；
2. 新 Cover 比旧 Cover 明确增加，例如 +25 mm；
3. Regenerate；
4. 不重建、不调用 YangTools modify；
5. 再调用 `inspect_rebar(createdId)`，并读取中心线到原 host face 的距离。

要求：

- Rebar Id 不变；
- RebarPlane 仍为 ToCover；
- 钢筋中心线相对原 face 的距离变化约等于 Cover delta；
- ShapeDriven / Standard / HostId 不变。

这一步直接验证“宿主 Cover 变化后钢筋自动适应”的核心假设。

完成后可以把 Cover 恢复原值，或者关闭不保存测试项目。

## 9. R02-E｜NumberWithSpacing

同一 face 创建：

- 6 positions；
- spacing 200 mm；
- 读回 NumberWithSpacing / 6 / 200 / ArrayLength 1000；
- RebarPlane 必须仍为 ToCover；
- first/mid/last sample 均存在。

## 10. R02-F｜错误输入

至少测试：

- invalid HostId；
- faceStableReference 为空；
- stable reference 无法解析；
- reference 属于另一个 Host；
- start=end 或投影后长度 <1 mm；
- number=0；
- spacing<=0。

如果方便找到 curved/concealed face，再测；找不到就 NOT_TESTED。

每项：明确失败 + Rebar 数量不增加。

## 11. 补 Undo 证据

上一轮失败是脚本没正确导入 PostCommand API。本轮按以下语义执行：

```python
from Autodesk.Revit.UI import RevitCommandId, PostableCommand
cmd = RevitCommandId.LookupPostableCommandId(PostableCommand.Undo)
uiapp.PostCommand(cmd)
```

注意：PostCommand 在当前 API context 返回以后才执行。

所以：

1. 创建一个专门用于 Undo 的 R02 Single；
2. 记录 count / Id；
3. 单独调用上面脚本，只 post Undo；
4. bridge 返回后等待 Revit idle 1~3 秒；
5. 再单独读取 count / Id；
6. 只允许最后一次产品创建被一次 Undo 完整撤销。

若 Revit 自动化环境仍不执行 PostCommand，写 `NOT_CONFIRMED`，不要使用删除元素模拟 Undo。

## 12. UI

UI 本轮不是阻塞核心门禁。必须证明：

- Ribbon 34；
- `钢筋工具区 / 按面保护层 R02` 已注册；
- 至少一张 Revit 2024.3 标题栏 + YangTools Ribbon 截图。

若自动化无法完成 Face Pick + 2 点 + TaskDialog，标 `UI_INTERACTIVE_NOT_TESTED`，不得让它阻塞 R02 Service/MCP 核心结论。

## 13. 回传

打包：

`RESULT_YANGTOOLS_REBAR_R02_MIMO_YYYYMMDD_HHMM.zip`

至少包含：

- RESULT.md；
- source/input SHA；
- build/restore/static logs；
- Build/Deploy/Loaded SHA；
- MCP raw JSON；
- R00 readbacks；
- face probe JSON；
- cover-change before/after JSON；
- Undo before/after；
- journals；
- screenshots；
- 若有源码改动：完整 diff；无改动也写 `NO_SOURCE_DIFF`。

结论只允许：`EXECUTION PASS / EXECUTION CONDITIONAL / EXECUTION FAIL`。最终 GO 由 ChatGPT 判定。
