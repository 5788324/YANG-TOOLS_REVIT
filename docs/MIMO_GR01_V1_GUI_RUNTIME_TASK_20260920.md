# Mimo Windows / Revit 2025 Task｜GR01 V1 GUI Runtime

日期：2026-09-20  
角色：Windows/Revit 实机执行器，不做最终 GO 判定。

## 1. 输入身份

必须使用本任务包内的 GUI 源码 ZIP。先记录源码 SHA；任何编译修复均记 `SOURCE_DIFF` 并提供 patch，不得静默改源码。

主运行环境：Revit 2025.4.x。  
Revit 2024：compile-only 0 error 即可。

## 2. Fresh Build / Deploy / Loaded

输出：

- 2025 Fresh Build 0 error；
- 2024 compile-only 0 error；
- Build DLL SHA；
- Deploy DLL SHA；
- Revit 实际 Loaded DLL SHA；
- 必须证明 Build=Deploy=Loaded；
- Journal/Revit release/API/PID/document title。

使用一次性测试文档，不修改正式 Bridge 模型。

## 3. 静态

运行：

- `Test-RebarGR01V1.py`
- `Test-RebarGR01V1Gui.py`
- `Test-RebarGR01P0.py`
- `Test-UiPanelsStatic.py`
- 全部 `tools/Test-*.py`
- `python -m py_compile McpServer/server.py`

不要给每个测试脚本盲传 `--root`；按脚本自己的 CLI 运行。

## 4. GUI-A｜Ribbon 与窗口

1. 打开 `钢筋工具区 -> 创建钢筋`；
2. 截图证明存在 `渐变钢筋集`；
3. 点击打开 WPF；
4. 截图证明窗口中文布局完整、无黑屏/控件重叠/文字裁切；
5. 再次点击同一 Ribbon 项，证明只激活现有窗口，不产生第二实例；
6. 钢筋区仍只有四个一级 Pulldown，不因新增功能重新横向溢出。

## 5. GUI-B｜控制筋 + ModelLine 拾取

使用已经证明 Core 的两根 single-position 控制筋或等价 fixture：

1. 点“拾取钢筋”选择 Start；窗口恢复并显示正确 Rebar Id/Type/Host/Positions；
2. 选择 End；
3. 路径类型选择“模型线”；
4. 点“拾取路径”，选择 12550mm 直线 ModelLine；
5. 窗口恢复并显示 ModelLine Id 与长度。

必须是真实按钮交互，不允许直接给窗口字段赋值冒充 GUI PASS。

## 6. GUI-C｜五区编辑与 Preview

将 Zone 表编辑为：

| 类型 | Length | MaxSpacing |
|---|---:|---:|
| 加密 | 1000 | 100 |
| 普通 | 4250 | 200 |
| 加密 | 2000 | 100 |
| 普通 | 4300 | 200 |
| 加密 | 1000 | 100 |

点击“计算预览”。

要求 GUI 可见：

- 有效路径 ≈ 12550mm；
- 86 intervals；
- 87 bars；
- Z1 100；
- Z2 195×21 + 155；
- Z3 100；
- Z4 180×23 + 160；
- Z5 100；
- Host/BarType 正确；
- “创建钢筋集”从 disabled 变 enabled。

同时保留产品日志 / raw result，确认 Engine=`ExplicitManagedFreeForm`、ChangedModel=false、fingerprint 非空。

## 7. GUI-D｜Preview 失效

Preview 成功后，把任意 Zone Length 临时改动 1mm。

要求：

- Create 立即 disabled；
- Preview summary/status 提示需要重新计算；
- 不允许用旧 fingerprint 直接创建。

恢复正确数值并重新 Preview。

## 8. GUI-E｜从 GUI Create + Undo

点击“创建钢筋集”。

要求：

- 产品 Success=true；
- 只增加 1 个 Rebar Element；
- PhysicalBarCount=87；
- Validation=Success；
- MaxReadbackDeviationMm 在既有 Core 容差内；
- 首末控制筋不变；
- GUI 显示成功 RebarId/数量；
- 成功后 Create 再次 disabled，要求重新 Preview。

执行一次 Revit native Undo：

- 新 GR01 set 消失；
- 控制筋仍在；
- Rebar count 恢复。

## 9. GUI-F｜Edge Path 拾取

复用 targeted Core 已证明过的真实直线 Edge fixture 或重新创建等价 DirectShape Edge。

必须通过窗口：

1. 切换路径类型为“模型 Edge”；
2. 点击“拾取路径”；
3. 在 Revit 中真实点选 Edge；
4. 窗口显示 `Edge · Owner ... · 12550 mm`；
5. Preview 得到 87/86 与相同五区规划。

本轮 GUI Gate 至少要求 Edge Preview PASS；若时间允许可再 Create+Undo，但 Core Edge Create 已在上一轮 Runtime GO，不要求重复作为 blocker。

## 10. GUI-G｜取消与错误输入

至少验证：

- 拾取时 Esc：窗口恢复，不崩溃；
- 未选择控制筋直接 Preview：窗口给出用户级提示，不写模型；
- Zone Length=0：Preview 前或产品层明确拒绝；
- 手动间距 > 最大间距：明确拒绝；
- 路径类型切换后旧路径被清空，Create disabled。

## 11. 最终清理

关闭 WPF，确认：

- 无成功 GR01 result 残留；
- 原控制筋存在；
- 临时 fixture 按需要删除；
- Revit 无未关闭模态/僵死窗口。

## 12. 结果包

输出：

`RESULT_YANGTOOLS_GR01_V1_GUI_MIMO_YYYYMMDD_HHMM.zip`

至少包含：

- RESULT.md；
- source/build/deploy/loaded SHA；
- build logs；
- static logs；
- Journal；
- GUI Ribbon/window screenshots；
- ModelLine pick / Preview / Create / Undo 原始证据；
- Preview invalidation 证据；
- Edge GUI pick / Preview 证据；
- final counts / no residue；
- `diff/NO_SOURCE_DIFF.txt` 或明确 SOURCE_DIFF patch。

执行方结论只能写：

`GR01 V1 GUI EVIDENCE COMPLETE — CHATGPT REVIEW REQUIRED`

或明确 `PARTIAL / NOT_TESTED`，不得宣布 Release GO。
