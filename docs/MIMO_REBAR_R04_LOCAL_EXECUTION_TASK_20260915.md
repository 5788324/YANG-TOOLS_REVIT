# Mimo Windows / Revit 实机任务｜YangTools Rebar R04

日期：2026-09-15
执行角色：Mimo（Windows / Revit 执行器）
最终 GO/NO-GO：ChatGPT

## 0. 输入与边界

只使用本离线包内源码：
`YangTools_Project_3.1.38_Source_REBAR_R04_STATIC_20260915.zip`

先核对 sidecar SHA。必须 fresh 解压到新目录。

禁止：
- Git push / PR；
- 顺手改其它 YangTools 功能；
- 把 NOT_TESTED 写成 PASS；
- 用旧 DLL / 旧截图替代本轮证据；
- 启动 Revit 2025（本轮 2025 只 compile-only）。

允许：如果 Revit API 签名导致**最小编译修复**，可以做最小修复，但必须回传 source diff、修复后源码 SHA，并将结论写 `EXECUTION CONDITIONAL`。

## 1. Fresh Build

- Revit 2024：必须 0 error；记录 warning 数；
- Revit 2025：compile-only 0 error；
- 运行全部 `tools/Test-*.py`；
- 目标至少：R00 44/44、R01 35/35、R02 44/44、R03 53/53、R04 50/50、UI 36 ribbon / 0 / 0；若数字因最小合法修复变化，说明原因。

## 2. 2024 加载签名

必须证明：
`Build SHA == Deploy SHA == Loaded module SHA`

Loaded path 必须从**运行中 Revit 2024 进程实际程序集/模块**取得，不可只读 `.addin`。

保存 PID、Revit version、document、loaded path、三 SHA。

## 3. 先补 R03-B：R00.3 transformed position

创建一个普通 `NumberWithSpacing` uniform set（建议 6 @ 200mm），然后调用 `inspect_rebar`。

硬门禁：
- `SchemaVersion=R00.3`；
- `SampleBarPositions` 的 `GeometryMode=TransformedCenterline`（若只得到 fallback，标 PARTIAL）；
- first / middle / last 的 Midpoint 不得全部重合；
- 实际位置差应与 200mm spacing / array length 一致到合理容差。

此门禁用于确认 R03-B 的 identical midpoint 是旧探针读回问题还是产品布置问题。

## 4. R04-A dryRun

对一个已经有 Edge/Start/End ToCover 的 R03 set 调：

```text
modify_shape_rebar(
  rebar_id=<id>,
  number_of_bar_positions=8,
  spacing_mm=150,
  dry_run=true
)
```

必须：
- Success=true；
- ChangedModel=false；
- before/after 模型 rebar count 与目标参数完全不变；
- BeforeInspection 可读。

## 5. R04-B 原位布局修改（核心）

对 R03 **Varying** set 做：`6@200 -> 8@150`。

必须证明：
- 同一 ElementId；
- 同一 UniqueId；
- 同一 HostId/HostUniqueId；
- LayoutRule=NumberWithSpacing；
- count=8；
- spacing≈150mm；
- `ConstraintsPreserved=true`；
- Edge / Start / End 仍 ToCover，目标宿主相同；
- varying 模式保持；
- first/mid/last 长度仍至少两根差 >2mm；
- transformed midpoint 沿分布方向展开。

## 6. R04-C BarType 修改（有安全夹具则测）

如果项目中有第二个可用 RebarBarType，或能用测试 fixture 安全 duplicate 出第二类型，则测试：
- 原位 ChangeType；
- ElementId / UniqueId / Host / Constraints 不变；
- 新 BarType readback 正确；
- 若直径不同，记录 centerline / cover 行为。

如果没有安全第二类型，写 `NOT_TESTED_NO_SECOND_BAR_TYPE`，**不阻塞 R04 Core GO**。

fixture 可用 Python 创建测试类型；产品修改动作必须走 `modify_shape_rebar` typed tool。

## 7. R04-D Undo

对 R04-B 再做一次独立修改，例如 `8@150 -> 7@175`。

随后通过 Revit `PostableCommand.Undo`，等待 idle，再 inspect：
- Rebar 仍存在；
- ElementId 不变；
- layout 恢复修改前 `8@150`；
- Constraints 仍在。

这一轮 Undo 验证的是“撤销修改”，不是撤销创建。

## 8. R04-E 错误矩阵

至少：
1. invalid rebarId；
2. ExpectedUniqueId 错误；
3. count=0；
4. count>1002；
5. spacing=0；
6. invalid BarTypeId；
7. no-op（目标参数等于当前）；
8. Free Form 输入（若无 Free Form 样本可 NOT_TESTED）。

所有失败必须：Success=false / 明确 error / 模型不变。

## 9. UI

真实点击 `钢筋工具区 -> 原位修改 R04` 最好验证；自动化无法稳定完成 PickObject / WPF 输入时可写 `UI_INTERACTIVE_NOT_TESTED`，不阻塞 Core。

截图若作为 2024 UI 证据，标题栏必须明确显示 **Autodesk Revit 2024.x**。此前 R02/R03 截图误取了 Revit 2025，不得重复。

## 10. 回传结果包

`RESULT_YANGTOOLS_REBAR_R04_MIMO_YYYYMMDD_HHMM.zip`

至少包含：
- RESULT.md
- build/ 2024 + 2025 logs
- static/
- sha/ source + build/deploy/loaded
- mcp_raw/
- rebar_json/ before/after
- undo_evidence/
- diffs/（NO_SOURCE_DIFF 或真实 diff）
- journals/
- screenshots/（仅真实、版本正确的图）
- fixture/（若创建第二 BarType）

Mimo 只能写 `EXECUTION CONDITIONAL`；最终 GO/NO-GO 由 ChatGPT 给出。
