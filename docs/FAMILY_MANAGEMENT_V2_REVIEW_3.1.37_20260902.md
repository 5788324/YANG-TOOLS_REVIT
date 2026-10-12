# YangTools｜族文档管理 / 族实例管理源码审查与 V2 增强建议

日期：2026-09-02
基线：YangTools 3.1.36 FINAL_E2E → 3.1.37 PasteCAD Shared 静态候选

## 一、结论

用户“功能不够强”的感觉成立。

当前两个窗口都有不少代码，但核心定位偏“表格参数编辑器”：

- `FamilyManagerWindow.xaml.cs`：约 1209 行；
- `FamilyInstanceManagerWindow.xaml.cs`：约 1258 行。

体量已经很大，但高价值的管理动作偏少，因此出现“代码不少、按钮不少、但实际管理效率不高”的问题。

本轮先审查，不盲目扩产品业务；建议下一轮独立做 **Family Management V2**。

---

# 二、当前“族文档管理”实际能力

源码当前具备：

1. 只列出 `Family.IsEditable == true` 的载入族；
2. 按 Category 分组/过滤；
3. 搜索族名；
4. 查看当前族全部 `FamilySymbol`；
5. 动态添加类型参数列；
6. 直接改类型名称；
7. 直接改可写类型参数；
8. Excel 导出/导入；
9. 复制类型；
10. 单族重命名；
11. 批量重命名类型，可选同步改族名；
12. 修改 Family Category（EditFamily → LoadFamily）；
13. 导出当前族为 RFA；
14. 删除类型；
15. 没选类型时可删除整个族。

因此它严格说不是“族文档管理”，更接近：

> **载入族 + 族类型管理器**

---

# 三、“族文档管理”主要缺口

## P0/P1：最影响日常效率

### 1. 没有“使用情况”

当前列表只有：

- Category
- FamilyName
- TypeCount

缺少：

- 项目实例总数；
- 已使用类型数；
- 未使用类型数；
- 是否整个族未使用；
- 当前视图实例数。

这导致不能直接回答“这个族有没有人在用”“哪些类型可以清理”。

### 2. 族和实例管理完全割裂

`FamilyInstanceManagerWindow` 构造函数其实支持传 `familyId`，但当前没有任何生产入口使用这个参数。

也就是说 Family Manager 已经具备“跳到指定族实例”的技术基础，却没有 UI 链路。

建议加入：

> **管理实例**

当前选中族 → 直接打开/激活族实例管理，并自动选中这个族。

### 3. 没有未使用类型清理

现有删除是手工选类型直接删，没有：

- 使用数；
- unused 过滤；
- 删除依赖预检；
- “仅清理未使用类型”。

建议复用族实例管理现有的 Trial Delete 思路，先预检，再删除。

### 4. RFA 能力太单一

现在只能：

> 当前单个族 → Export RFA

缺少：

- 多族批量导出；
- 从文件重载族；
- 从文件夹按族名批量重载；
- 新旧 RFA 对比；
- 重载结果报告。

对于真正的“族文档管理”，这是最明显缺口。

### 5. 没有族内部信息

当前参数表其实读的是项目里的 `FamilySymbol` 参数，不是 Family Document 的 `FamilyManager` 参数模型。

缺少：

- Family Parameter 名称；
- 类型/实例参数；
- Shared GUID；
- Formula；
- Group；
- 参数数据类型；
- Nested Families；
- Connector/Material/Host/WorkPlane 摘要。

因此“族文档”三个字目前名不副实。

---

# 四、“族文档管理”代码层问题

## 1. 参数身份只用名称

多个位置使用：

```csharp
LookupParameter(pName)
```

风险：项目存在重复同名参数时，名称不是稳定身份。

YangTools 本身已经有“重复项目参数清理”，所以这里尤其应该避免继续把 Name 当唯一键。

建议 V2 引入 `ParameterKey`：

优先级：

1. BuiltInParameter；
2. Shared Parameter GUID；
3. ParameterElementId / Definition identity；
4. 最后才用 Name 作显示和兼容。

### 2. 可用参数只从第一个类型读取

`CreateDynamicColumns()` 只取：

```text
family.GetFamilySymbolIds().First()
```

因此不同类型存在差异参数时可能漏列。

应改为所有 symbols 的参数 identity union。

### 3. 列配置按 FamilyName 文件名保存

例如：

```text
FamilyConfig_<FamilyName>.txt
```

族改名后旧配置失联；同名族/不同上下文也可能冲突。

建议存 JSON，并至少组合：

```text
Category + FamilyName
```

或者使用项目内 Family UniqueId 做“本项目配置”，另存名称模板用于跨项目。

### 4. 删除族/类型没有依赖预检

Family Instance Manager 已有 Trial Delete → RollBack → 展示级联对象。
Family Manager 没有同等级保护。

建议统一复用一个 `SafeDeletePreviewService`。

### 5. code-behind 已过大

1209 行窗口代码继续加功能会明显增加回归风险。

不建议做全面 MVVM 重构，但建议抽出三个服务：

- `FamilyUsageService`
- `FamilyTypeEditService`
- `FamilyFileService`

UI 只做显示和 ExternalEvent 调度。

---

# 五、当前“族实例管理”实际能力

## Loadable Families 页

- 只列项目里有实例的族；
- 按族选择；
- 根据 TypeName 分组；
- 搜索 TypeName / ElementId；
- 动态实例参数列；
- 直接改单个实例参数；
- 从视图 PickObjects 建选择过滤；
- Excel 导出/导入；
- 选中行 → Revit 视图选择/定位；
- 删除实例，并先 Trial Delete 检查级联删除。

## System Families 页

硬编码支持 10 类：

- Walls
- Floors
- Ceilings
- Roofs
- Pipes
- Ducts
- Cable Tray
- Conduit
- Stairs
- Railings

同样支持动态参数、Excel、选择、删除。

---

# 六、“族实例管理”主要缺口

## P0/P1：建议优先做

### 1. 没有真正的范围选择器

当前 `_filterBySelection` 同时影响 Collector 是否限定 ActiveView。

用户界面没有清晰的：

- 全项目
- 当前视图
- 当前选择

三个 scope。

建议明确做一个 Scope ComboBox，取消隐式语义。

### 2. 没有“批量换类型”

这是族实例管理最缺的高频动作之一。

建议：

- 选中多行；
- 目标类型只列当前 Family 合法 FamilySymbol；
- 一次 TransactionGroup；
- 每实例 SubTransaction；
- 失败逐项汇总；
- 一次 Ctrl+Z。

系统族也可以使用合法 ElementType + ChangeTypeId，但需要按类别单独验证。

### 3. 没有批量设同一个参数值

当前表格是“逐单元格编辑”。

实际高频需要：

> 选 N 个实例 → 选参数 → 输入一次值 → 批量写入

建议支持：

- 覆盖全部选中；
- 仅填空值；
- 失败跳过并报告；
- 一次 Ctrl+Z。

### 4. 过滤维度太少

当前基本只有：

- Family
- TypeName 搜索
- ElementId 搜索

建议至少增加：

- Type；
- Level；
- Host；
- Workset；
- Pinned；
- 当前视图可见。

不用一次做所有复杂规则，V2 先做 Type + Level + Scope 就很有价值。

### 5. 缺少位置/状态信息

建议表格固定列可选显示：

- Level；
- Offset；
- Host；
- X/Y/Z；
- Rotation；
- Pinned；
- Mirrored；
- Workset。

其中先只读显示，后续再开放安全可写项。

### 6. 不支持多族联合管理

当前 Loadable Family 页一次只能看一个 Family。

“选择图元”虽然可以 Pick 多个，但逻辑会锁到第一实例所属族。

建议增加：

> 当前选择模式

允许不同 FamilyInstance 一起进入表格，按 Category → Family → Type 分组。

这对批量检查/统一参数非常实用。

---

# 七、“族实例管理”代码层问题

## 1. 参数 identity 同样只靠 Name

大量：

```csharp
inst.LookupParameter(pName)
```

应与 Family Manager 一起改为稳定 `ParameterKey`。

## 2. 参数列表只取第一实例

`CreateDynamicColumns()` 和 `CreateSystemDynamicColumns()` 遇到第一实例后立即 `break`。

因此参数集合并非当前结果集的 union。

## 3. Loadable/System 两套动态列代码高度重复

两套方法各约 130 行，逻辑基本相同。

这是后续增加筛选/批量赋值时最容易产生“两边行为不同”的地方。

建议抽成共用 builder/service，而不是继续复制。

## 4. 系统类别写死

当前只有 10 个 BuiltInCategory。

这不一定是 Bug，但“系统族管理”能力上限被代码硬编码。

建议 V2 用“允许实例编辑的系统类别集合 + 实际项目存在类别”动态生成，而不是把 UI 永久锁死为 10 类。

---

# 八、建议的 V2 功能范围

为了避免又做成大而杂的窗口，建议分两级。

## V2.1：高频效率版（建议先做）

### 族文档管理

1. Instance Count；
2. Used Type Count / Unused Type Count；
3. 只看未使用；
4. 管理实例（跳转）；
5. 批量导出 RFA；
6. 清理未使用类型（预览 + 一次 Undo）；
7. 参数 identity 改为稳定 key。

### 族实例管理

1. Scope：全项目 / 当前视图 / 当前选择；
2. Type filter；
3. Level filter；
4. 批量换类型；
5. 批量设置参数；
6. 固定状态列：Level / Host / Pinned；
7. 稳定 ParameterKey；
8. 保留现有 Excel / Select / Safe Delete。

## V2.2：真正“族文档”版

后续再做：

1. RFA 文件/文件夹重载；
2. Project Family ↔ RFA compare；
3. Family Parameter / Formula / GUID 面板；
4. Nested Family 依赖；
5. Host / WorkPlane / Connector / Material 摘要；
6. 批量 Family Document 规则检查。

---

# 九、不建议本轮做的东西

暂时不建议：

- 全面 MVVM 重写；
- 自建族版本数据库；
- 云端族库；
- AI 自动决定参数/族类型；
- 自动修改复杂 Formula；
- 自动替换 Nested Families。

这些会明显扩大风险，但对当前个人高频工作提升不如 V2.1。

---

# 十、建议下一轮验收重点

## Family Manager

- usage count 与实际实例严格一致；
- unused type 判定正确；
- 清理 unused 前 Trial Delete，失败无残留；
- 批量导出 RFA 文件数/文件名正确；
- “管理实例”准确跳到同一 Family；
- 重复同名参数不误写。

## Instance Manager

- Scope 三模式精确；
- Type/Level filter 数量正确；
- 批量换型前后实例数不变；
- 批量参数写入真实值正确；
- 每批一次 Ctrl+Z；
- group/hosted/pinned 等不允许修改的实例安全失败并报告；
- Excel 旧功能不回退。
