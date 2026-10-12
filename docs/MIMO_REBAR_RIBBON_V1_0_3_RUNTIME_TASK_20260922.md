# Mimo Task — Rebar Ribbon V1.0.3 Revit 2025 Smoke

日期：2026-09-22
最终 GO owner：ChatGPT

## 目的

只验证 Ribbon 分组和图标，不重复 GR01 Create/Undo、ZAxis、ModelLine、Edge 或性能测试。

## 必须检查

1. Fresh build Revit 2025，记录 Source SHA、Build/Deploy/Loaded SHA，SOURCE_DIFF 必须显式报告。
2. 打开 YangTools → 钢筋工具区。
3. 一级只显示两个钢筋 Pulldown：
   - `创建钢筋`
   - `钢筋测试`
4. `创建钢筋` 展开后只有 `渐变钢筋集`。
5. `钢筋测试` 展开后有其余 12 个命令；不得出现旧的 `修改钢筋 / 端部与连接 / 检查钢筋` 一级入口。
6. 截图证明两个 Pulldown 图标可见且不是旧 `project_inspector` 占位图。
7. 抽查至少 5 个钢筋测试子命令的独立图标，确认不是全部相同。
8. 点击 `渐变钢筋集`，窗口能正常打开即可；无需再次创建钢筋。

## 输出

- 真实 Revit 标题栏 + Ribbon 截图；
- 两个 Pulldown 展开截图；
- Source/Build/Deploy/Loaded SHA；
- `NO_SOURCE_DIFF` 或完整 diff；
- 执行结论只能写 `TEST EXECUTION COMPLETE / PARTIAL / BLOCKED`，最终 GO 由 ChatGPT 判定。
