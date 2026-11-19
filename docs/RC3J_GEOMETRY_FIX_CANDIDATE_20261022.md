# RC3J Geometry Fix / Ribbon UX 后续候选（2026-10-22）

基线是 `YangTools_RC3J_RibbonUX_STATIC_CANDIDATE_20261022.zip` (632 files，B608B405CC0BCCBDB238A347C9B50A9721CA66DB07458B18585C4CF1DDA2D969)。源 RC3J 619 文件 Git/PR 未修改。

## 本次只改三个 2D 命令

- `直线合并`：把原先的 `UnionFind` 简单端点关联改为共用二维直线 Planner；要求整个线段在 0.1 mm 容差内，拓扑度数是开放简单链（两个自由端点），不能有正长度重叠、T 分叉或闭环。预选过滤不足两条时允许点击后重选。
- `重叠清理`：正长度交叠才连接，使用两条线端点与参考轴的双向垂直误差而不是固定 `AngleTolerance=1e-7`；重叠区间做并集，遇到不满足统一参考轴的链式漂移直接跳过；预选过滤不足两条允许后选。
- 两个工具都优先保留每组最长的一条原 DetailCurve.ElementId，对它用 `SetGeometryCurve(curve, false)` 调整，其他冗余线才调用 `Document.Delete`。提前扫描 Dimension.References 和 `GetDependentElements(null)`；存在引用/依赖的整个候选组跳过。意外连带删除或几何读回偏差会触发事务回滚。此机制是已知 API 的保守保护，不是任意未知外部引用 100% 安全保证。
- `隐藏边界`：预选、后选均可；从 `GetValidLineStyleIdsForFilledRegion` 有效线型中优先查系统 `OST_InvisibleLines`（避免英文硬编码），只在唯一匹配时调用 `SetLineStyleId`。应用后不意味着独立叠加详图线也会消失。填充对象不重建。
- 更新这三个功能的 `tooltip/usage`、36 项功能目录与 `RIBBON_FEATURE_DEVELOPMENT_RULES.md`。

## 非目标与严格限制

- 没有改 CAD、文字、其他 33 个工具、YangCAD Exchange、main/Draft PR、API 2027 适配。
- Geometry Planner 遇到不明标注、特殊约束、曲线相依关系会宁可跳过或整体回滚。此阶段更重视防止误删，而不是追求最大成功率。
- 无 Windows SDK/Revit GUI：**SOURCE STATIC GO；BUILD NOT_TESTED；RUNTIME/UNDO NOT_TESTED；RELEASE NO-GO**。旧 RC3J 六版本 F1 编译通过的结论不得转移至这个新包。
- Gemini 只能在隔离 QA 环境执行编译/部署/测试，不能碰当前 Revit2023 生产工作环境。其原始证据须独立审核才可改变状态。
