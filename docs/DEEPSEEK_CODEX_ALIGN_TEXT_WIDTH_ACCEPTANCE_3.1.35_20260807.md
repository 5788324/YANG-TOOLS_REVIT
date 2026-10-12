# DeepSeek/Codex 执行任务：文本基准对齐段落宽度编译与专项验收

## 角色与限制

你负责 Windows 编译、部署和 Revit GUI/模型实测。用户不参与手工测试。

- 源码基线：`YangTools_Project_3.1.35_Source_TEXT_WIDTH_20260807`
- 不推送 Git。
- 不新增其它功能。
- 只修真实编译器或 Revit 实机发现的问题。
- 每个 Revit 年份使用独立 DLL，禁止跨年份复用。
- 测试使用项目副本，所有成功写入必须验证一次 `Ctrl+Z` 完整撤销。

## A. 源码与静态检查

在全新目录解压源码后运行：

```powershell
.\Verify-SourceBaseline.cmd
python tools\Test-UiPanelsStatic.py --root .
python tools\Test-ReviewFixes.py --root .
python tools\Test-GeneratorLevelPlacement.py --root .
python tools\Test-ParameterCleanupPipeline.py --root .
python tools\Test-AlignTextWidth.py --root .
```

要求所有脚本退出码为 0，并保留 JSON 输出。

## B. Windows 编译

1. 先编译 Revit 2024 Configuration，保存：
   - `restore.log`
   - `build.log`
   - `build.binlog`
   - DLL 大小、文件版本和 SHA-256
2. 若本机装有其它 Revit 版本，按 2021–2027 对应 Configuration 独立编译。
3. 缺 SDK、API 包或 Revit 的年份标记“未完成/未实测”，不得推断通过。
4. 编译错误只允许定点修复，修复后重新执行全部静态检查。

## C. Revit 2024 GUI 验收

打开：`YangTools → 文本工具区 → 基准对齐`。

确认窗口包含并正确显示：

```text
段落宽度相同（复制源文字框宽度）
```

复选框默认不勾选。选择源/目标后窗口重开时，勾选状态必须保留。

## D. Revit 2024 功能矩阵

准备至少 8 个 TextNote，覆盖：

- 单行和多行；
- 左对齐、居中、右对齐；
- 同一 TextNoteType 和不同 TextNoteType；
- 有引线和无引线；
- 宽文本框和窄文本框。

逐项执行：

1. **只同步宽度**
   - 取消 X、Y、旋转，只勾选段落宽度。
   - 目标 `TextNote.Width` 应与源相同。
   - 目标 Coord、旋转角、文字内容、TextNoteType 不应改变。

2. **宽度 + X/Y**
   - 验证目标插入点与源对应坐标一致，同时 Width 一致。

3. **宽度 + 旋转**
   - 验证旋转角和 Width 均与源一致。

4. **四项全选**
   - X、Y、旋转、Width 均按源设置。

5. **多目标**
   - 一次选择至少 5 个目标；成功数应正确，所有可处理目标 Width 一致。

6. **不同文字类型允许范围**
   - 选择一个目标类型，使源 Width 超出其允许范围。
   - 该目标应回滚并跳过，其它目标继续成功。
   - 完成提示应包含目标 ID 和宽度范围原因，不得静默夹取。

7. **锁定/成组/受约束目标**
   - 失败目标回滚，其它目标继续。
   - 文档最终 `IsModifiable=false`。

8. **换行表现**
   - 记录同步前后截图。目标内容不变，但因 Width 改变而重新换行属于正常结果。

9. **撤销**
   - 每种成功组合执行一次 `Ctrl+Z`，目标 Coord、旋转和 Width 全部恢复。

## E. 数值证据

对每个测试目标记录：

- ElementId；
- TextNoteType；
- 操作前后 `Coord.X/Y/Z`；
- 操作前后旋转角；
- 操作前后 `Width`；
- 源 `Width`；
- 目标允许最小/最大 Width；
- 成功/跳过及原因。

Width 使用 Revit 内部英尺原值记录，同时可附毫米换算。通过条件：成功目标 `abs(target.Width - source.Width) <= 1e-9 ft`。

## F. 交付证据

返回：

1. 修改后的完整源码 ZIP；
2. 编译日志、binlog 和 DLL；
3. DLL SHA-256；
4. 上述专项测试表；
5. 截图、Journal、YangTools 日志；
6. 实际修复 diff；
7. 更新后的 README、PROJECT、WORKLOG、FEATURE_AUDIT、兼容性说明和 HANDOFF；
8. GO / 条件 GO / NO-GO；
9. 完整证据包 SHA-256。

缺少原始编译日志、DLL、截图或数值前后对比的“PASS”不采纳。
