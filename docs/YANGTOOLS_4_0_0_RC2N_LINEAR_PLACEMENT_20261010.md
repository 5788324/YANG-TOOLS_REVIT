# YangTools 4.0.0 RC2N｜模型修改区：线性布置安全与高程闭环

日期：2026-10-10；输入 RC2M SHA `0E4B5739AB2A19DA4E43B589DF7291E6C7172D34C62E7A46D07E0D968D421394`。

## 实施范围
仅修改 `LinearPlacementEngine`、`LinearPlacementWindow.xaml.cs`，新增 `LinearPlacementRequest` / `LinearPlacementResult`，升级一条旧标高契约测试并新增 RC2N 136 项源码/数学检查。保留 `FamilyPlacementHelper` 的严格真实世界 Z 检验、现有中文 XAML、既有 MCP/Rebar/Ribbon/installer/csproj。

## 确认的问题与修复
- `ExternalEvent` 原执行时直接引用可变 `LinearPlacementViewModel` 以及活动文档。改由 WPF 点击时 `Capture` 冻结参数及曲线列表、符号 ID、标高 ID；`RevitEventHandler(_doc, wrapInTransactionGroup:false)` 绑定原项目，关闭或新请求后不执行旧回调。`Create` 可供后续非 WPF 入口复用，尚未完成 MCP tool。
- 旧 `CalculatePlacements` 在起终点颠倒时先计算出负间距、直接静默生成 0 个，之后才交换首尾，导致错误。现改为明确拒绝倒置，并提示用反转路线选项。固定数量须整数、正数、≤ 10000；固定间距直接预估数量再分配，含剩余终点的数量也受限，避免爆内存/死循环。
- 过去 `SortAndChainCurves` 连接不到的曲线仅弹提示却继续使用部分路线。现不允许遗漏不连通曲线；提交前再次检查逐段首尾连接关系、切线非零；路径仍只支持单条连续开放路线，不承诺自动分支/闭环/线交叉处理。
- 保持相对标高世界坐标 `targetLevel.ProjectElevation + relativeElevationFeet`。首尾以**实际放置的首末实例**作为高程插值端点，跨多条曲线使用累计弧长。单实例仅使用起点相对高程，不宣称可同时满足两个不同端点高程。
- Revit 事务 `Start/Commit/RollBack` 显式验证；模型确认提交后按 ElementId 回读类型、位置 1 mm 容差。回读失败返回警告而非报告“已回滚”，结果 UI 提示异常只记录并告知模型已提交。
- WPF 输入 Binding 存在 Validation 错误时拒绝布置，拒绝 NaN、±Infinity、非法的范围、间距、数量及过期符号标高。

## 验收及限制
- 静态专项 Test-ModelModifyRC2N.py 138/138，其中独立数学模型校验 25 例；保留旧 Test-LinearElevationProfile.py 世界 Z 矩阵 20/20。
- 旧 52 套 + RC2N 新增 1 套 = 53 套；工作区/最终 ZIP 并发独立执行 53/53，顺序执行因运行时限中断，报告不得写成连续运行完成。
- Windows C# 编译、Revit Loaded、实际多段曲线、非零基准标高、模型位置、UI 交互及 Ctrl+Z **NOT_TESTED**。**4.0.0 RELEASE NO-GO**，正式稳定版 3.1.36 FINAL_E2E。
- 后续模型修改区顺序：标高修改 → 基于面转换 → Loft → 实体提取 → 布尔几何，且保留 Excel/管理器 Fill/Copy 等 backlog。
