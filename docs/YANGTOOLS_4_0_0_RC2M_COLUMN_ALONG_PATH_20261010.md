# YangTools 4.0.0 RC2M｜沿线布柱安全/UI 请求快照（2026-10-10）

## 单轮目标
来自 RC2L。把模型修改区提到优先级最高；本轮仅 `ColumnAlongPathWindow` 和 `ColumnAlongPathEngine`，不调整既有路线重排、旋转、标高参数设置算法；不动 XAML、MCP、Rebar、Ribbon 和工程目标。

## 源码确认风险
- 旧 `StartPlacement_Click` 向 `_handler.SetAction` 排入闭包，却在执行时直接读取可变 `_viewModel`；用户连续操作或关闭窗口后可用到新状态。
- Engine 旧逻辑 `transaction.Start()` 未检查状态；普通 `catch` 忽略回滚返回状态；Commit 后才发生的 Selection/Refresh/UI 错误会被外层 ExternalEvent 误报建模失败。
- WPF 非法 double 输入可能保留上一次合法 ViewModel 值；核心仅校验少量负值/NaN；极小间距可让 `while` 生成海量柱或不收敛。

## RC2M 修改
- 新 `ColumnAlongPathPlacementRequest.Capture` 在 WPF 点击时复制路线列表快照、符号/标高 ElementId、间距/里程/偏移/旋转等基础值，不在 Revit API 事件里读可变 VM；Engine 按 typed request 执行。
- `ColumnAlongPathPlacementResult` 仅事务确认为 Committed 后返回创建的 ID/首末里程；提交后逐项查找生成族实例/类型，并将未知回读作为警告，而非“已回滚”。
- `Transaction.Start` 检查 Started；失败时显式核对 RollBack；无法确认事务状态时不报告回滚成功。
- 所有数值拒绝 NaN/Infinity；无效 WPF 数字输入拒绝提交；布置数量限制 10,000/批（超限明确提示并建议分段），累积浮点计算改按 index*spacing。
- WPF 关闭及新请求覆盖旧 ExternalEvent 后不执行/不修改界面；路线选择、加载列表也守卫过期窗口；提交后选择/刷新失败明确提示模型已成功写入而非重试。

## 禁止误判
- Windows C# Build、Revit Loaded、实际柱类型/标高/旋转、项目模型回读/Undo 均需独立实机证据；本轮静态通过不代表 Runtime GO。
- 不确认完整路桥模型线与环形闭合路线支持，原路线限定为水平开放多段线不变。
- `MCP` 仅要求后续可复用核心 Request/Service；本批未新增 MCP Tool。
