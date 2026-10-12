# YangTools Rebar R01｜Mimo 结果独立复核

日期：2026-09-15  
复核者：ChatGPT  
输入：`RESULT_YANGTOOLS_REBAR_R01_MIMO_20260915_1054.zip`  
结果包 SHA-256：`3EAC0B98DD90D789D3E4917058DBC96DFE202727EB30290A163E69F9BDBCC894`

## 结论

**R01 CORE RUNTIME GO / UI+UNDO OPEN / RELEASE NO-GO**

允许进入 R02 `PlanarFace + Host Cover + ToCover` 开发。R01 不作为正式 YangTools 发布候选。

## 已独立核实

- 输入源码 SHA：`94A32D1C498AFC5A6078C2B0817BDE415367F0C84158FE342206478F0CC9A2E4`，与 ChatGPT R01 原始源码包一致。
- Revit 2024 build：0 error / 253 warning；本轮无源码修改。
- 静态：R00 44/44、R01 35/35、UI 33 Ribbon / 0 error / 0 warning。
- Build / Deploy / Loaded-path-file SHA：均为 `E32F43768B2DCB79322B852EC8FC78F7C9D3482F10214B09993DA457E1505FE7`。
- 运行中程序集路径由 Revit 2024 MCP/IronPython 从 loaded assembly 取得，不再仅凭 `.addin` 推断。
- MCP 状态明确返回 `revitVersion=2024`、文档 `Project1`。
- dryRun：`ChangedModel=false`，未创建 Rebar。
- Single：Id `321976`，ShapeDriven、Standard、正确 Host、Single、4/4 constrained handles。
- Inspector R00.2 已正确读取 `RebarStyle=Standard`。
- NumberWithSpacing：Id `321979`，6 positions @ 200 mm、ArrayLength 1000 mm、first/mid/last 三个 sample 均存在且长度 2000 mm。
- 7 项错误输入全部 `success=false`；错误矩阵后 Rebar 数量仅包含三次成功创建。
- `screenshots/revit2024_before_ui.png` 标题栏为 Autodesk Revit 2024.3 / Project1，截图版本正确；但它只证明会话，不证明 R01 Ribbon 按钮完整交互执行。

## Undo 复核

Mimo 将 Undo 标为 NOT_CONFIRMED 是正确的，不能升级为 PASS。

证据顺序：

1. 10:42:14 创建 Id 321976；
2. `undo1.json` 失败：`PostCommandId` 未定义；
3. `count_after_undo.json` 仍为 1；
4. 再创建 Id 321977；
5. `count_after_ctrlz.json` / `count_after_ctrlz2.json` 仍为 2；
6. 10:44:33 才创建钢筋集 Id 321979。

因此没有证据证明一次 Ctrl+Z 已撤销 R01 Transaction。

下一轮可用官方 postable Undo 入口重试：

`RevitCommandId.LookupPostableCommandId(PostableCommand.Undo)` → `uiapp.PostCommand(...)`。

PostCommand 会在当前 API context 返回后执行，所以必须在桥返回后等待，再单独读取 Rebar 数量。

## UI 复核

- Journal 证明 Ribbon command 已注册；
- 截图证明 Revit 2024.3 会话；
- 没有完成 `PickObject(Face/Element) + PickPoint + TaskDialog` 的真实点击链。

故：`UI REGISTERED = PASS`，`UI INTERACTIVE FLOW = NOT_TESTED`。

## 对 R02 的影响

R01 已经证明创建层最关键的闭环：

`request → validation → Transaction → CreateFromCurves → Layout → Commit → R00 readback`

R02 可以只新增“几何/约束层”，不需要重写 R01 创建链。
