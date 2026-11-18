# YangTools 4.0.0 — Exchange P5 独立证据审查

日期：2026-11-18。审查范围：用户上传 `P5_EVIDENCE.zip` 与本会话现有 P5 源码 ZIP（不是再运行一次 Revit）。

## 结论
- **F1 BUILD GO（Revit 2021–2026）**：6 份日志退出码均为 0、0 个错误；6 份 DLL 本体的大小和 SHA-256 与汇总全部匹配。
- **F2 Revit 2025 C4 LIMITED GO（可用的部分转换）**：真实 `CadToRevitCommand.Execute` 的 QA 执行记录为 `Succeeded`；创建后目标视图 1,131 个图元；Journal 有交易成功记录与原生 `ID_BUTTON_UNDO`，撤销后视图 0 个图元；未见 `666 iterations`。
- **不是 100% 恢复或最终几何全部合格**：29/71 个 HATCH 未创建，1/58 个尺寸未创建，1/109 个文字因为纯空白被拒绝。另有 1 条已创建线条，在后续尺寸事务后偏移 0.18755 mm，大于 P5 0.15 mm 上限，但源码审计报告中没有记录该最终超限改变。
- **正式全项目 Release 仍 NO-GO**：其他年份 Runtime 没有实测；YangCAD 全链路与其它工具整体发布未完成。

## 证据完整性
- ZIP 大小：4,066,561 bytes；SHA-256：`63129CD5C9A6B9B0325F44919DE1F943895DF82D0D3D9BA4EBFFC4DF804DFFF8`。
- ZIP 成员：32；CRC PASS；`SHA256SUMS.txt` 31/31 PASS。
- P5 源码包：653 项，ZIP SHA-256 `DB3749667CD74B42FF39A6C39B7FEF4C0F190825E5E41598F24E3BCBCCC38B43`；证据里的 653 项 before/after 清单完全相同，且均匹配源码 ZIP 原始字节。
- DLL 2021、2022、2023、2024、2025、2026 均在证据 ZIP 中，六份大小和 SHA 与 `all_builds_summary.json` 一致。
- 原始重导出 C4 MAP 文件本体**没有**放入本 ZIP；`1D506D76...` 的哈希来自 QA 计算结果，不能通过这个 ZIP 单独重新计算 MAP 哈希。

## 全量 SourceId / ElementId 对账
- 审计报告原始：841 条源对象，841 个唯一 SourceId，810 创建、20 创建失败、11 Reader 拒绝。
- 创建分类：曲线源 603/603，生成普通 DetailCurve 810；文字 108/109；填充 42/71；尺寸 57/58，生成尺寸参考线 114。
- 创建图元合计：108 TextNote + 810 DetailCurve + 42 FilledRegion + 57 Dimension + 114 尺寸参考线 = **1,131**。
- 逐源报告中的 ElementId 共 1,131 个，全部唯一；其中普通 DetailCurve 810 条，与 `P5_C4_ALL_810_CURVES.csv`/`.json` 的 SourceId+ElementId 集合相同。
- `audit_warnings` 39 条，其中 38 条 `CAD_AUTOJOIN_MICROADJUST` 与最终 810 行里 `status=accepted` 的 SourceId、ElementId 和 MaxDriftMm 一一匹配。

## 最终几何例外（唯一明确遗留）
- 源 `SourceId=1ED`，Revit `ElementId=1060243`，普通 Line。
- 期望长度 100.00000 mm；最终长度 100.18755 mm；一个端点变化约 0.18755 mm，**超过 P5 设定上限 0.15 mm**。
- CSV/JSON 的最终实测标记为 `status=rejected`、`max_drift_mm="Infinity"`（这里的 rejected 是**事后几何判定**，不是 Reader/创建结果；逐源转换报告中该对象仍为 `created`）。
- 810 条最终状态：771 strict + 38 accepted（其中最大 0.120010 mm）+ 1 rejected；这个 rejected 未包含在原转换 JSON 的 39 条 `audit_warnings` 中。
- P5 的提交后安全断言在 `CreateCurves()` 事务后调用；后续 `CreateDimensions()` 创建参考线可能再次触发 Autojoin。因此不能将 **线条事务后** 810/810 合格扩展为 **全流程完成后** 810/810 合格。
- 证据不显示任何图元丢失或再次回滚；这是最终几何保真报告遗漏，而不是 C4 转换失败。

## 后续建议（按个人项目优先级）
1. **冻结 P5 为 Revit 2025 的可用但非无损候选**，不为单条 0.18755mm 偏移继续无限扩大 P6/P7。
2. 在 Draft PR #9 记录 `1ED → 1060243` 的最终超限问题；今后有实用需要时，最小改动是在全部事务完成后再次核验所有新建详图线，并将最终超限写入报告/给出警告，不必首先添加重试或调大容差。
3. 用户试用 C4 转换结果时重点检查上述线条、29 处未建填充及短标注；可选择接受部分转换。不要标注为“无损导入”或“所有对象完全恢复”。
4. 停止重复六版本编译；其他版本的运行时、YangCAD 全链路和个人可用版本打包放到下一阶段独立处理。

Git 最新基线：P5 Draft PR #9 / Commit `17a94952a8f2adb0255deb98fff8e564864c6f57`。本轮无源码修改、无 Revit 操作、无主分支合并。