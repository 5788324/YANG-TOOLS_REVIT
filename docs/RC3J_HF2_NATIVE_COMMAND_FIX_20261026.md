# YangTools RC3J HF2 — 原生命令链路修复（源码候选，未编译）

来源：2026-10-23 Gemini `GEMINI_F2_NATIVE_20261023.zip` 及 HF1 636 文件源码。仅对 J01/O01/O02/O04 命令链路阻断和正交 T 分支修正。

## 变更
1. `ProtectedLineIds()` 忽略 `GetDependentElements(null)` 返回的当前元素自身 ID，保留对其他所有依赖 ID 和真实 Dimension 引用的保护。
2. `直线合并`/`重叠清理` 命令在确认后、进入 `DetailLineCleanup.Apply()` 前备份并清空 UI 预选；成功时选中保留线，失败时尝试恢复有效的原选项。**不放宽** `Document.Delete()` 级联删除保护：`removed.Count != 1` 时依然回滚。
3. `JoinPlans()` 在端点链内部节点（度数 2）检查本次选中的全部有效详图线，发现非共线分支触及节点则拒绝该链。只扫描选中有效线；未选中分支的全视图查找不在此补丁范围。

## 证据边界
- 旧版 HF1：2021–2026 BUILD GO；HF1 CORE 部分通过，但原生命令 J01/O01/O02/O04 Cancelled。
- 此版本 HF2：仅源码静态候选；BUILD/Runtime/Undo 均 NOT_TESTED。
- `GetDependentElements` 无法穷尽所有被其他元素引用的情形。必须对真实 Dimension、Group、Pinned 及 Document.Delete 级联行为做 F2。
- 当删除返回多个 ID 时不假定多余 ID 必为选择句柄。即使清空选择仍出现多 ID，也**必须回滚并记录 ID/类型**，不得放行。
- Gemini 旧 `before/after/undo` 使用 TransactionGroup.RollBack，不等于 Ctrl+Z。
- 原始 Journal、部署记录不在本次 F2 上传 ZIP 内；不得宣称其已被独立验收。
- 禁止复用已修改的生产 Revit 2023、禁止覆盖稳定插件，不自行同步 Git。
