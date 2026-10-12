# YangTools 3.1.35 族材质参数绑定

更新：2026-08-21

## 当前两个入口

### 1. 绑定材质（简易）

面向日常使用：先选择一个或多个项目 `FamilyInstance`，再输入材质参数名、选择实例/类型参数和当前项目已有 Material。YangTools 自动编辑相关族、绑定几何材质参数、回载项目，并立即把选定 `Material.Id` 写入所选实例或对应族类型。

- 默认实例参数，只影响当前选择实例；
- 类型参数会影响项目中全部使用相同类型的实例；
- 无预选时自动进入多选 FamilyInstance；窗口支持“重新选择实例”；
- Material 只能从当前项目现有材质中选择/搜索，本轮不自动新建项目材质；
- 若没有任何目标成功赋材质，整次操作回滚；
- 族编辑 + 项目赋值由一个外层 `TransactionGroup` 聚合，目标为一次 Ctrl+Z。

### 2. 批量绑定材质

原 2026-08-14 的 Family 列表批处理工具保留并改名“批量绑定材质”。适合一次处理大量族，只负责建立/统一几何 Material 与族材质参数的关联，不主动选择某个项目 Material。2026-08-21 同时修复了参数名称输入框布局。

## 核心实现

1. 项目中选择 Family；就地族、不可编辑族不执行。系统族不属于本工具的可编辑载入族范围。
2. `Document.EditFamily` 打开独立族文档。
3. 扫描族文档非类型元素的 `BuiltInParameter.MATERIAL_ID_PARAM`。
4. 若同名族参数不存在：
   - Revit 2022+：`FamilyManager.AddParameter(name, GroupTypeId.Materials, SpecTypeId.Reference.Material, isInstance)`；
   - Revit 2021：`FamilyManager.AddParameter(name, BuiltInParameterGroup.PG_MATERIALS, ParameterType.Material, isInstance)`。
5. 同名参数已存在时必须同时满足：
   - 参数数据类型为 Material；
   - 类型/实例方式与本次选择一致。
   否则该族失败，禁止自动 ReplaceParameter，避免破坏公式、标签或既有引用。
6. 对每个几何 `Material` 参数使用 `FamilyManager.AssociateElementParameterToFamilyParameter` 关联目标材质族参数。
7. 若选择“覆盖已有绑定”，对已有其它材质参数关联先安全解除，再重新绑定；不可解绑/不可关联项记录为跳过。
7.1. 关联查询/解除/可关联性使用运行时能力探测；若目标版本缺少关联反查 API，则不把“无法反查”误判成绑定失败，真实版本由实机再次验证关联结果。
8. 族文档事务提交后通过 `LoadFamily` 回载项目；`IFamilyLoadOptions` 设置 `overwriteParameterValues=false`，优先保留项目中现有族参数值。
9. 回载后验证 Family/FamilySymbol/FamilyInstance ElementId 与已有正 ID 自定义参数内部值未发生静默变化，并验证目标材质参数已出现在对应族类型或已有实例上。

## 行为边界

- 本工具**只建立参数和几何关联，不自动赋某个具体 Material 值**；项目用户后续可在族类型或实例上选择材质。
- 空心体、引用元素、不可关联参数等不会强制修改。
- 嵌套族内部几何属于嵌套族自己的族文档，不会被宿主族间接改写；如需绑定，应同时选择对应嵌套载入族单独处理。
- 如果族中没有任何带 `MATERIAL_ID_PARAM` 的元素，不创建无用参数，直接跳过。
- 同一个批次使用项目 `TransactionGroup` 聚合回载；回载后的数据完整性异常触发整批回滚。

## 当前状态

- 源码已实现；
- 静态专项检查通过后方可交给 Windows 编译；
- Revit 2024/2025 必须真实验证类型参数、实例参数、覆盖已有绑定、多族批量、无材质实体族和 Ctrl+Z。
