# YangTools 4.0.0 RC2V｜视图/文本/标注合并 Bug/UI 安全审查（2026-10-12）

基线：RC2U SHA `FD8C69348119EA2063D83521916E94D2B00E96220EC980FD3380648645C7C43F`。

## 本轮实质改动

1. `ViewGraphicCleanerWindow`：覆盖计数失败不再按 0 算/缓存为已计算；可重试；搜索过滤造成的隐藏勾选在执行前确认；清理之前对全部视图一次性扫描与校验，单 TransactionGroup 内按视图写入、回读清理结果，任何操作失败回滚整个组并区分回滚不确定；提交后才更新列表 UI，界面失败不再把已提交的清理谎报失败；无覆盖时不创建 Undo。
2. `MergeTextCommand`：事务启动/提交检查，逐个删除 TextNote 校验，回滚失败时不会继续；已提交后的 Revit Selection/Refresh 失败作为 UI 警告单独处理，避免模型已合并却显示“合并失败”。
3. `DimTextOverrideCommand`：事务启动检查；任何标注变成无效图元或非 Dimension 时整批报错，杜绝静默 continue 后报告全部成功；处理数核对；保留原 TransactionHelper.ShowSuccessAndCommit 的显式 Commit 状态检查。
4. `DistributeTextWindow`：大于零的间距之外明确拒绝 NaN/Infinity，避免错误数值传入几何变换。

## 综合审查边界

- 已审阅 AlignTextToText、AlignTextToLine、MergeText、DistributeText、DimTextOverride、MicroToolEngine、ViewGraphicCleaner 及相关窗口。仅修证据明确的路径；AlignTextToText 现有按目标子事务部分成功语义、临时插件箱 DLL 依赖/隔离逻辑本轮保留。
- 清理操作仅清除视图 `By Element` 覆盖，不代表清除 View Template、Category/Filter 或相位设置；当前界面按同步方式计算较大模型时可能阻塞。实机性能 / 视图类型支持未验证。
- Revit API 下 `Document/Views` 的各种 IsValidObject/TransactionStatus / GetElementOverrides 运行行为、元素依赖删除、Undo、GUI 可用性、跨 Revit 2021–2027 均 **NOT_TESTED**。本轮 Python 源码测试不等于 C# 编译。
- 为兼容已有宿主，未改 Rebar、Ribbon、MCP 27 命令线协议、csproj/installer，无新 MCP 端点，不使用 Mimo、不推 Git，也不要求用户人工执行 Revit 测试。

下一阶段优先临时插件箱及剩余小工具的专项风险收口（DLL 依赖、安全目录/插件说明），之后其他项目管理模块；Windows 编译/加载/撤销仍是独立发布门禁。
