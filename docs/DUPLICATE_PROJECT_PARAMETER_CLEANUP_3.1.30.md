# YangTools 3.1.30 重复项目参数清理

## 入口

```text
YangTools → 检查工具区 → 重复项目参数清理
```

按钮和窗口名称均为“重复项目参数清理”。功能已整合到 YangTools 主 DLL，不再单独注册 `RevitParameterDeduplicator.addin`。

## 界面

- 使用 YangTools WPF 主题资源，跟随暖光米白、深色、专业蓝和 Apple 四套主题。
- 结果表启用行/列虚拟化，适合 600+ 重复组。
- “运行输出与预览报告”为只读可选择文本框，可以拖选和复制。
- 底栏“功能说明”位于“关闭”旁边。
- 图标：`parameter_cleanup_16.png`、`parameter_cleanup_32.png`。

## 扫描预览

扫描完成后自动输出：

1. 项目参数总数、唯一参数名称数。
2. 当前不重复参数数量。
3. 重复参数总数、重复名称组数。
4. 可自动处理组、不兼容组。
5. 当前选中的重复组和来源参数数量。
6. 理论清理后参数总数、剩余重复组和不重复参数数量。
7. 按共享参数与已知引用计算的保守可删除预估。
8. 已知至少仍会保留的重复组，以及预计最多形成的不重复参数数量。

理论预览不伪装成最终结果。保守预览会单独显示“已知至少仍会有多少重复组”；参数值冲突、全局参数关联和 `Document.Delete()` 级联对象只能在执行阶段确认，因此实际剩余重复组可能更多。

## 清理后复核

快速处理和单独删除完成后会自动重新扫描，并输出：

- 项目参数清理前后数量。
- 重复组清理前后数量。
- 不重复参数清理前后数量。
- 唯一名称数量。
- 本轮实际成功删除数量。
- 清理后是否仍有重复参数。
- 剩余重复组示例。

CSV 报告新增：

```text
07_清理后复核.csv
```

## 固定算法原则

### 保留原参数，禁止全部删除重建

每组选择一个现有 `ParameterElement` 作为保留参数：

```text
合并绑定类别
→ 将来源值复制到保留参数
→ 删除确认可删除的来源 ParameterElement
```

不采用“导出全部值 → 删除所有同名参数 → 重建参数 → 导入值”。

### 按参数 ID 识别

不使用 `LookupParameter(name)`。同名参数可能有多个，必须使用 `ParameterElement.Id` 建立映射。

### 按元素单次遍历

```text
实例元素：一次 FilteredElementCollector
元素类型：一次 FilteredElementCollector
```

遍历每个元素自身参数，根据 ParameterId 字典匹配重复组。不会执行“606 个参数组 × 全项目扫描 606 次”。

3.1.30 进一步复用内层字典和 HashSet，避免每个元素创建多层字典、List 和 Tuple。

### 原始存储值直接复制

| StorageType | 读取 | 写入 |
|---|---|---|
| Double | `AsDouble()` | `Set(double)` |
| Integer | `AsInteger()` | `Set(int)` |
| String | `AsString()` | `Set(string)` |
| ElementId | `AsElementId()` | `Set(ElementId)` |

`AsValueString()` 只允许用于界面/报告显示，不能用于回填。

### 减少 Revit 数据库写入

- 保留参数绑定类别已经等于所有来源类别并集时，不调用 `BindingMap.ReInsert`。
- 本轮没有任何 ReInsert 时，不调用额外 `Document.Regenerate()`。
- 合并和删除放在一个 `TransactionGroup` 中，快速处理最终形成一个 Revit 撤销项。

## 最低保护

为了尽快上线，没有增加企业级审批流程，只保留：

1. 共享参数默认不删除，可手动放开。
2. 参数值冲突默认不删除，可手动放开。
3. 明细表、视图过滤器、全局参数关联、写入错误会保护对应来源参数。
4. 删除每个来源参数时检查 `Document.Delete()` 返回的全部 ID；出现任何额外 ID，当前 `SubTransaction` 回滚，其他参数继续。

## 多版本兼容

- Revit 2021 使用 `Definition.ParameterType`、`Definition.ParameterGroup`。
- Revit 2022+ 使用 `Definition.GetDataType()`、`GetGroupTypeId()` 和 `ForgeTypeId`。
- `BindingMap.ReInsert` 通过兼容层调用对应年份的分组参数。
- ElementId 使用 YangTools 现有 long 适配。

必须逐年份独立编译，不跨年份复制 DLL。


## 安装迁移

3.1.30 已并入 YangTools 主 DLL。图形安装器和 `tools/Build-All.ps1 -Deploy` 会自动移除本项目历史独立插件：

```text
RevitParameterDeduplicator.addin
RevitParameterDeduplicator\
```

避免功能区同时出现独立版和 YangTools 内置版。
