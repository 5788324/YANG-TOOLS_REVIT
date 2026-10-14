# YangTools 4.0.0 RC3B｜图框换型事件队列 / 批处理 UI 收口

日期：2026-10-13
来源：唯一 RC3A 完整源码（SHA-256 4B79D0987CB2DFB94E88543139471DC87E0E8007534F355D45D8FD058CFEB7C5）。

## 问题一：图框换型 pending 永不清理

`SheetManagerWindow` 为每个图框实例维护一个 HashSet pending 锁。
`RevitEventHandler.Execute` 在文档切换/关闭时会跳过排队 action；旧 `finally` 块不会执行，因此界面会保留 pending 锁。恢复文档后同一实例不能再次提交换型；此外原先未检查 Raise 拒绝或同步异常。

**修复**：在 RevitEventHandler 上新增可选 `Action<string> onSkipped`（不改变原调用点）和 `CancelPendingAction(operationId)`，对已取消的队列项在取出时跳过并写诊断日志。文档不匹配时调用 onSkipped 使 UI 恢复；Raise=Pending 因同个处理器队列已经排队而不重复 Raise，Rejected / TimedOut / 异常时取消动作并恢复选择。callback 自身异常不会影响队列后续任务。

## 问题二：图框换型提交结果不确定

换型前确认当前文档有效、可写，实例和目标类型均为图框。提交前 Regenerate 并读取 Symbol.Id，提交后再重新查找图框并读取 Symbol.Id。界面结果区分“未确认提交”和“已提交但结果需核对”，杜绝提交后 UI 失败时简单恢复旧类型造成误导。

## 问题三：批处理完成状态误报

导出主路径执行成功后，打开目录可能抛异常；之前被外层 catch 捕获，给用户显示“批处理执行中断”。现在将打开目录作为独立 best-effort 后处理，把失败加入 Warning 并反馈到报告和结果弹窗。写入报告失败也应告知用户，不能静默返回空路径。批处理全批预检和偏好保存异常有独立捕获，确保 busy 按钮状态恢复且不把设置保存视作导出失败。

## 测试与限制

`tools/Test-RC3B-QueueBatchUI.py`：静态源码合约。执行完整 Python `Test-*.py`、XAML XML 解析、新 ZIP fresh 复测及受保护目录 SHA 变化检查。没有 Windows dotnet/csc 或 Revit 环境，不能认定真实事务、ExternalEvent 调度、WPF UI 交互和 Undo 通过。

## 后续实机用例（由 Windows/Revit 执行代理，不要求用户手测）

1. 打开项目 A 的图框管理器，发起换型后立即切到项目 B，确认不会错误修改项目 B；返回 A 后可以重试；检查同步触发的窗口关闭。
2. 对同一图框快速连续选多种类型，仅保留有效的在途任务，确认列表最终显示真实 Revit Symbol.Id。
3. 触发 `Raise` Denied/TimedOut 以及读回异常（注入测试）确认 pending 清理、操作不被延迟执行。
4. 批处理实际导出 NWC/IFC/DWG/PDF，模拟报告目录不可写、OpenFolder 失败与用户偏好保存失败；核查输出存在且 UI 告警准确。
5. 实机 Build + Revit Loaded + 退回旧类型后的 Undo + 2021/2024/2025/2027 兼容矩阵（不能将一次实测扩大为全版本结论）。

## 保护边界

不新增 MCP wire，不改钢筋、Ribbon、安装器或 csproj；模型改动只能在 Revit ExternalEvent 主线程中执行。
