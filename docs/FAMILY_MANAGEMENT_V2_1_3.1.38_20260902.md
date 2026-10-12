# YangTools 3.1.38｜Family Management V2.1

日期：2026-09-02

## 1. 定位

本轮不重写现有两个大型 WPF 管理窗口，而是在原有 Excel/动态参数表格能力上补齐真正高频的“管理动作”，并把 UI 尺寸作为产品契约而不是验收后再修的小问题。

涉及：

- `项目管理区 → 族文档管理`
- `项目管理区 → 族实例管理`

3.1.38 同时继承 3.1.37 的 `从 CAD 粘贴 → 对齐共享坐标`，该功能仍需 Windows/Revit 实机闭环。

---

## 2. 族文档管理 V2.1

### 新增使用统计

左侧每个族显示：

- 类型数
- 项目实例数
- 未使用类型数

右侧当前族显示：

- 项目实例数
- 使用中的类型 / 总类型
- 未使用类型数
- 当前视图实例数

统计由 `FamilyUsageService` 一次扫描项目 `FamilyInstance` 构建，不再逐族重复全项目 collector。

### 新增过滤

- 仅显示“存在未使用类型”的族
- 当前族的类型表仅显示未使用类型
- 保留类别过滤与族名搜索

### 管理实例

新增“管理实例”按钮：

`族文档管理 → 当前 Family → 族实例管理`

若对应族实例管理窗口已经打开，不新建第二个 ExternalEvent 队列，而是复用现有窗口并切换到目标 Family。

### 多族批量导出 RFA

左侧支持 Ctrl/Shift 多选族后一次选择目录批量导出：

`Document.EditFamily → SaveAs(.rfa) → Close(false)`

- 非法文件名自动清理；
- 同名文件统一一次确认覆盖；
- 单族失败记录后继续其它族；
- 不在项目文档创建无意义 Transaction。

### 安全清理未使用类型

只允许清理 `UsageCount == 0` 的 FamilySymbol。

执行前必须：

1. 重新扫描最新使用状态；
2. `Trial Delete`；
3. RollBack；
4. 若 Revit 返回除目标 Symbol 之外的级联删除 ElementId，自动清理被阻止；
5. 无额外级联后才让用户确认真实删除。

真实删除为单 Transaction，因此目标是一次 `Ctrl+Z`。

### 稳定 ParameterKey

动态参数列不再把显示名称当作主要写入身份。

优先级：

- Shared Parameter GUID → `GUID_*`
- BuiltInParameter → `BIP_*`
- Parameter ElementId → `PID_*`
- 无 API 身份时才使用编码后的 `NAME_*`

重复同名参数在列头显示身份后缀；无法唯一定位时拒绝写入，不允许 `LookupParameter(name)` 随机命中。

旧 `.txt` 参数列配置会在可唯一匹配时迁移到 Stable Key。

---

## 3. 族实例管理 V2.1

### Scope

明确增加三种范围：

- 全项目
- 当前视图
- 当前选择

“选择图元”完成后自动切换到当前选择范围。

系统族页同样遵守范围过滤。

### 快速过滤

全局固定过滤：

- Type
- Level

搜索同时可匹配 Type / ElementId / Level / Host。

### 固定状态列

两个 Tab 都固定前 5 列：

1. ElementId
2. Type
3. Level
4. Host
5. Pinned

后面才是用户动态参数列。

### 批量换类型

对表格当前选中行执行：

`Element.ChangeTypeId(targetTypeId)`

规则：

- 一个外层 Transaction；
- 每个实例一个 SubTransaction；
- 单个实例失败只回滚该实例；
- 全部失败则外层 Transaction RollBack；
- 成功批次一次 Ctrl+Z；
- 对可能被 Revit 替换 ElementId 的 ChangeTypeId，读取返回 replacementId 后再校验真实新类型。

### 批量写同一参数

选择动态参数 + 输入值后可批量写入当前选中行。

可选：

`仅填空值`

规则：

- Stable ParameterKey 定位；
- `ParameterValueHelper` 按项目当前显示单位解析 Double/Integer；
- 每个实例一个 SubTransaction；
- 单项失败继续；
- 成功批次一次 Ctrl+Z。

### System Family 动态类别

删除旧版固定类别清单。

系统族 Tab 根据当前项目真实存在的：

- 非 `FamilyInstance`
- Model Category
- 有有效 TypeId

动态发现可管理类别。

实际 ChangeTypeId 仍由 Revit API 决定是否合法，非法项逐项失败，不猜测转换。

### Excel

实例 Excel 新增固定只读信息：

- 标高
- 主体
- 固定

动态参数表头使用 Stable Parameter DisplayName；导入时对重复同名无法唯一识别的参数明确失败。

---

## 4. UI 尺寸硬门禁

### 族文档管理

窗口：

- `1280 × 780`
- Min `1080 × 650`

布局：

- 左侧族列表 `340 px`
- 右侧 Header `82 px`
- 底部工具栏 `52 px`
- DataGrid Row `30 px`
- Header `36 px`
- 动态参数列 `160 / min 140 / max 280 px`

固定列：

- Type Name `220 / 170 / 320`
- Usage `82 / 72 / 100`
- Status `90 / 82 / 110`

窗口缩小时：

- 不允许按钮自动挤成多行；
- 底部工具栏使用横向 ScrollViewer；
- DataGrid 使用横向滚动；
- 长族名和状态文字不允许挤压右侧按钮，标题区域使用受约束 Grid + TextTrimming。

### 族实例管理

窗口：

- `1360 × 800`
- Min `1160 × 660`

布局：

- 全局筛选/批量区 `86 px`
- Tab 内筛选行 `46 px`
- 底部工具栏 `52 px`
- DataGrid Row `30 px`
- Header `36 px`
- 动态参数列 `160 / min 140 / max 280 px`

固定列：

- ElementId `92 / 82 / 110`
- Type `190 / 150 / 280`
- Level `130 / 110 / 190`
- Host `180 / 130 / 260`
- Pinned `70 / 64 / 80`

批量控件：

- Target Type `220 px`
- Parameter `230 px`
- Value `190 px`

UI 实机验收必须同时检查正常窗口和缩到 MinWidth 的布局，不能只验证默认尺寸截图。

---

## 5. 事务与线程边界

- 两个窗口继续绑定原 Document 的 read/write ExternalEvent；
- Revit API 读取放在 RevitReadEventHandler；
- 写操作放在 RevitEventHandler；
- 不新增第二个 WPF Application；
- 不改现有主题系统；
- 批量写入采用 Transaction + per-item SubTransaction；
- 删除类功能继续优先 Trial Delete；
- 成功批次以“一次 Ctrl+Z”为目标。

---

## 6. 本轮不做

V2.1 暂不加入：

- 完整 Family Document 参数/公式编辑器
- 嵌套族依赖图
- Connector 编辑器
- Workset/Phase 批量管理
- 坐标/旋转批量编辑
- 大型 MVVM 重构

这些保留到 V2.2，以免与本轮高频管理动作混合扩大回归面。
