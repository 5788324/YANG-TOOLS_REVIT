# YangTools 3.1.30 重复项目参数清理更新报告

## 更新范围

独立 `RevitParameterDeduplicator` 已整合到 YangTools 主项目，不再作为单独 Add-in 发布。

正式入口：

```text
YangTools → 检查工具区 → 重复项目参数清理
```

Ribbon 总按钮数由 27 增加到 28。

## UI 更新

- 名称统一为“重复项目参数清理”。
- 新增 16/32 像素参数收敛清理图标，沿用 YangTools 深蓝灰/浅蓝工程线条规范。
- WinForms 独立界面改为 YangTools WPF 主题窗口。
- 跟随暖光米白、深色、专业蓝、Apple 四套主题。
- 结果表启用行列虚拟化。
- 底部“功能说明”按钮位于“关闭”旁边。
- 运行输出为只读可选择文本，可拖选和复制。

## 扫描预览报告

扫描后自动输出：

- 当前项目参数总数。
- 唯一名称数量。
- 当前不重复参数数量。
- 重复参数总数和重复组数。
- 可处理组和不兼容组。
- 当前选中的来源参数数量。
- 理论清理后的参数总数、重复组和不重复参数。
- 按已知引用和共享参数保护计算的保守预估。
- 已知至少仍会剩余的重复组。
- 预计最多形成的不重复参数数量。

冲突、全局参数关联和 Revit 级联删除只能在执行阶段确定，因此预览会明确标为理论值/保守值，不冒充最终结果。

## 清理后复核

快速处理或单独删除完成后自动重新扫描，并输出：

- 项目参数清理前后数量。
- 重复组清理前后数量。
- 不重复参数清理前后数量。
- 唯一名称数量。
- 成功删除和受保护/回滚数量。
- 清理后是否仍有重复参数。
- 剩余重复参数名称示例。

新增：

```text
07_清理后复核.csv
```

## 算法与性能更新

### 按元素单次遍历

- 实例元素：一个 `FilteredElementCollector`。
- 元素类型：一个 `FilteredElementCollector`。
- 使用 `ParameterId → DuplicateGroupPlan` 字典匹配。
- 复用内层字典和 HashSet，减少 600+ 参数组场景中的临时对象。

### 不删除重建

每组保留一个现有 `ParameterElement`：

```text
合并绑定类别
→ 复制来源值
→ 删除确认可删除的来源参数
```

禁止“全部删除同名参数后重建”。

### 不通过显示字符串转存

值复制固定使用：

| StorageType | 读取 | 写入 |
|---|---|---|
| Double | AsDouble | Set(double) |
| Integer | AsInteger | Set(int) |
| String | AsString | Set(string) |
| ElementId | AsElementId | Set(ElementId) |

`AsValueString()` 只用于界面和 CSV 显示。

### 减少数据库写入

- 类别集合已经一致时跳过 `BindingMap.ReInsert`。
- 无 ReInsert 时跳过额外 `Document.Regenerate()`。
- 快速处理使用 `TransactionGroup`，形成一个 Revit 撤销项。

## 删除保护

保留快速上线所需的最低保护：

- 共享参数默认不删除，可手动放开。
- 冲突来源默认不删除，可手动放开。
- 明细表、过滤器、全局参数关联和写入错误保护对应来源。
- 每个来源参数独立 `SubTransaction`。
- `Document.Delete()` 返回额外 ElementId 时立即回滚该来源，其他来源继续。

## 安装迁移

图形安装器以及：

```powershell
.\tools\Build-All.ps1 -Versions 2021 -Deploy
```

会自动删除本项目历史独立插件入口：

```text
RevitParameterDeduplicator.addin
RevitParameterDeduplicator\
```

避免功能区出现两套相同功能。

## 验证状态

静态检查：

- 文本文件：223 个。
- C#：141 个。
- XAML：39 个。
- XAML 根节点：34 个。
- Ribbon 命令：28 个。
- 主题：4 套。
- XAML、资源、事件处理器、UTF-8：错误 0，警告 0。

当前环境不能执行 Windows/.NET/Revit 真实编译。Revit 2021 编译部署、主题、快速处理、清理后复核与 Undo 需由 DeepSeek 按验收任务完成。
