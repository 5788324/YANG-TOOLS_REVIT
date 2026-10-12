# YangTools 3.1.35 简易绑定材质

日期：2026-08-21

> 当前源码随后只做了线性布置 P0 真实 Z 门禁加固，没有改变本功能业务逻辑。简易/批量材质静态专项仍分别 31/31；Windows/Revit 2024/2025 实机仍待执行。

## 目标

提供比“批量绑定材质”更轻量的日常操作：用户直接选择项目中的一个或多个族实例，YangTools 自动编辑这些实例所属的载入族，建立材质参数与几何 Material 的关联，重新载入项目，并立即把用户选择的项目材质赋到所选实例或对应族类型。

## 操作流程

1. 在项目中预选一个/多个 `FamilyInstance`，或启动命令后用 Revit 多选实例。
2. 打开 `绑定材质` 小窗口。
3. 输入材质参数名称，默认 `材质`。
4. 选择参数方式：
   - 实例参数：默认；只给当前选中的实例赋材质。
   - 类型参数：给选中实例对应的 `FamilySymbol` 赋材质；同类型其它实例会一起变化。
5. 在 `赋予材质` 下拉中搜索并选择当前项目已有 `Material`。
6. 可选择是否覆盖族几何已有的其它材质参数绑定。
7. 确认后执行。

## 族编辑链

简易版不复制一套新的族编辑算法，而是复用批量版：

`EditFamily → Material FamilyParameter → AssociateElementParameterToFamilyParameter → LoadFamily`

`FamilyMaterialBindingService` 新增 `ExecuteWithinTransactionGroup`，让简易版可以在一个外层 `TransactionGroup` 内同时完成族编辑和项目材质赋值，目标为一个 Revit Undo 项。

## 材质赋值

回载族后，使用用户选择的 `Material.Id`：

- 实例参数：逐个对所选 `FamilyInstance` 的目标材质参数 `Set(Material.Id)`；
- 类型参数：对所选实例的 `FamilySymbol.Id` 去重后逐类型 `Set(Material.Id)`。

写入后立即用 `AsElementId()` 校验实际值。

每个目标使用 `SubTransaction` 隔离；单个目标失败记录原因，其它目标可继续。如果最终没有任何目标成功赋值，抛错并回滚整个外层 `TransactionGroup`，防止项目只留下空的材质参数绑定。

## 材质来源

第一版只允许当前项目中已经存在的 `Material`：

- ComboBox 支持输入搜索；
- 必须解析为完整的现有材质名；
- 不自动创建新 Material。

原因：避免在名称相似或用户只是搜索时产生垃圾材质。

## 与批量版的区别

| 功能 | 绑定材质（简易） | 批量绑定材质 |
|---|---|---|
| 选择入口 | 项目实例 | Family 列表 |
| 适合场景 | 日常快速处理 | 大批量族治理 |
| 建立材质参数绑定 | 是 | 是 |
| 立即赋项目材质 | 是 | 否 |
| 默认参数方式 | 实例参数 | 类型参数 |
| 当前 Ribbon 名称 | 绑定材质 | 批量绑定材质 |

## 数据安全

- 只处理载入族；内建族/不可编辑族由底层服务跳过。
- 同名参数不是 Material，或现有 Material 参数的类型/实例模式与本次选择不同：失败，不自动删除重建。
- 回载沿用批量版 Family/Symbol/Instance ID 和正 ID 自定义参数值完整性检查。
- `LoadFamily` 使用 `overwriteParameterValues=false`。
- 族编辑和材质赋值在外层 `TransactionGroup` 中聚合。

## 当前状态

- 源码实现：完成。
- 简易版静态专项：31/31。
- 批量版静态专项：31/31。
- UI/XAML 静态：0 错误、0 警告。
- Windows/Revit 编译与实机：未执行当前源码。
