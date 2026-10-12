# YangTools 3.1.35 — 文本基准对齐段落宽度同步

日期：2026-08-07

## 用户需求

在“文本工具区 → 基准对齐”中增加一个复选框。勾选后，目标文字的段落框宽度与源文字一致，语义对应 Revit API 的 `TextNote.Width`。

## UI

新增复选框：

```text
段落宽度相同（复制源文字框宽度）
```

默认不勾选，避免改变既有行为。X、Y、旋转和段落宽度可以任意组合，也允许只同步段落宽度。

## 执行规则

1. 读取源文字的 `TextNote.Width`。
2. 对每个目标文字读取其文字类型允许的最小、最大宽度。
3. 源宽度在目标允许范围内时，设置目标 `TextNote.Width`。
4. 设置后重新读取目标宽度，必须与源宽度在 `1e-9 ft` 容差内一致。
5. 只复制段落框宽度，不复制文字内容、TextNoteType、字号、字宽因子或局部格式。
6. 宽度改变可能导致目标文字重新换行，这是预期行为。

## 数据与事务安全

- 每个目标使用独立 `SubTransaction`。
- 目标被锁定、成组、受约束，或源宽度超出目标文字类型允许范围时，只回滚该目标。
- 不静默夹取到目标最小/最大宽度；无法精确匹配时按失败处理。
- 其它目标继续执行，最终提示成功数、跳过数和最多 8 条目标 ID/失败原因。
- 外层仍为一个 Revit `Transaction`，成功结果可以一次 `Ctrl+Z` 撤销。

## 影响文件

- `Models/TextToolStates.cs`
- `UI/AlignTextToTextWindow.xaml`
- `UI/AlignTextToTextWindow.xaml.cs`
- `Commands/AlignTextToTextCommand.cs`
- `tools/Test-AlignTextWidth.py`
- `tools/Verify-SourceBaseline.ps1`

## 当前状态

- 源码实现：完成。
- 静态契约：完成。
- Windows 编译：未执行。
- Revit 2024 GUI/模型验收：未执行。
- 正式发布：NO-GO，需完成专项实机验收。
