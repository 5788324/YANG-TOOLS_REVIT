# Mimo Task — GR01 V1.0.2 UI/UX Revit 2025 Smoke

日期：2026-09-20
最终 GO 判定：ChatGPT

## 范围

本轮只验证 V1.0.2 UI/UX 修复，不重复 GR01 Core Create/Undo/性能验收。

禁止修改产品源码；如必须修改，停止并回传 SOURCE_DIFF。

## A. Identity / Build

记录：
- Source ZIP SHA；
- Revit 2025 fresh build 0 error；
- Revit 2024 compile-only；
- Build=Deploy=Loaded DLL SHA；
- Revit version/API/PID/document；
- `NO_SOURCE_DIFF`。

## B. 鼠标滚轮（必须）

打开 `渐变钢筋集`：
1. 鼠标放在窗口普通区域滚轮，窗口上下滚动；
2. 鼠标放在 Zone DataGrid 上、表格自身无足够行可滚时，滚轮仍能推动整个窗口；
3. 临时添加足够多 Zone 让 DataGrid 自己出现纵向滚动：
   - 表格未到底时滚 DataGrid；
   - DataGrid 到底后继续向下滚，外层窗口接管；
   - 回到顶部同理。
4. 无卡死、无异常关闭。

截图/短录屏至少覆盖 DataGrid hover 场景。

## C. 方向语义（必须）

### ZAxis
- 拾取 Start / End；
- GUI 显示 `Rebar Start -> Rebar End`；
- 不显示 `+Z/-Z` 作为独立业务方向；
- 交换首末后方向文字反转，Zone[0] 仍说明从 Start 一侧开始。

### ModelLine
- 拾取一条直线 ModelLine；
- GUI 显示 `基准总长`；
- Preview 后显示 `有效长度`；
- 业务方向仍显示 `Start Rebar -> End Rebar`，不使用 ModelLine Endpoint0/1 作为用户方向。

Edge GUI 交互已由用户豁免，不要求本轮补测。

## D. Auto Remaining UX（必须）

初始行：
- 长度模式 = `自动剩余`；
- 未 Preview 前长度显示 `待计算`；
- 长度不可直接编辑；
- 列标题有 mm 单位。

完成 Preview 后：
- 自动行显示解析后的实际长度；
- 添加固定加密区后自动剩余实时扣减。

## E. 即时校验（必须）

至少验证：
- 最大间距=0 -> Zone 下方立即出现错误，Preview disabled；
- 恢复有效值 -> 错误消失，条件齐全时 Preview enabled；
- 已知有效长度后，固定区段超配 -> 立即提示且 Preview disabled。

## F. 新增区段位置

选中某 Zone 后点击 `+普通区` 或 `+加密区`，新行应插入选中行之后，而不是始终追加到末尾。

## G. 不需要重测

- GR01 Core 87/69 bar Create；
- one Undo；
- ModelLine Core Create；
- Edge Core；
- 252/503 性能；
- MCP AutoLength；
- ZAxis Core Create。

这些已有独立 Runtime GO。

## 结果标签

成功：
`GR01 V1.0.2 UI/UX SMOKE COMPLETE — CHATGPT REVIEW REQUIRED`

失败：
`GR01 V1.0.2 UI/UX PARTIAL — <explicit blocker>`

不得宣布 YangTools project-wide Release GO。
