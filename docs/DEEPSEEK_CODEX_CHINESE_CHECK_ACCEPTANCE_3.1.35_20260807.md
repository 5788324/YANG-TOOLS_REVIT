# DeepSeek / Codex：YangTools 3.1.35 中文检查与清理编译部署与专项验收任务

## 0. 唯一基线

以同批交付的：

`YangTools_Project_3.1.35_Source_CHINESE_CLEANUP_20260807.zip`

为唯一源码事实来源。必须解压到全新空目录，不覆盖旧源码目录，不推送 Git。

用户不参与手工测试。需要 GUI 的步骤由本机自动化/Codex 完成并保存证据。

## 1. 源码门禁

依次执行：

```powershell
python tools/Test-UiPanelsStatic.py --root .
python tools/Test-ChineseCleanup.py
python tools/Test-ReviewFixes.py
python tools/Test-BatchLinkPathMatching.py
python tools/Test-AlignTextWidth.py
python tools/Test-GeneratorLevelPlacement.py
python tools/Test-BatchPrintPdf.py
python tools/Test-ParameterCleanupPipeline.py
```

要求：中文清理 67/67；UI 0 错误 0 警告；其它现有契约不得回退。

## 2. 编译

本轮修改了 `ChineseCheckWindow.xaml/.cs` 和命令元数据，旧 DLL 不能复用。

至少独立构建：2021、2022、2023、2024、2025、2026。2027 若当前机器仍缺 .NET 10 SDK，保留 NETSDK1045 并标记 NOT_BUILT，不得写 PASS。

每年保存：
- restore.log
- build.log
- build.binlog
- DLL 大小与 SHA-256
- error / warning 数量

## 3. 部署

优先部署 Revit 2024 和 2025；若 2021 已安装，同步部署 2021 验证 net48 老版本兼容。

构建 DLL 与部署 DLL SHA-256 必须一致。

## 4. GUI 专项

### A. 基础 UI

1. 打开“检查工具区 → 中文检查”。
2. 标题为“项目中文检查与清理”。
3. 左侧分类列表可滚动，至少显示：族、族类型、系统类型、材质、视图、图纸、参数、房间、链接等。
4. 右侧只有一个统一 DataGrid，切换分类时数据与数量同步。
5. 搜索有效。
6. 全选 / 全不选 / 反选仅影响当前显示结果。

### B. 扫描覆盖

准备测试项目，至少人为制造中文：
- 族、族类型、墙/楼板等系统类型
- 材质
- 模型组或详图组
- 视图、视图模板、图纸名称、图号
- 明细表名称和列标题
- 修订说明/日期/签发人/签发给
- 标高、轴网、过滤器
- 用户工作集（工作共享项目）
- 设计选项、范围框、填充/线型图案、用户子类别
- 项目参数/共享参数名称
- 类型/实例字符串参数值
- TextNote
- 房间/空间/面积名称或编号
- 项目信息字符串
- Revit/CAD 链接名称（有条件时）
- 打印设置、视图/图纸集、DWG 导出设置

逐项确认出现在正确分类，不把数值 `AsValueString()` 当成文本命中。

### C. 双击重命名/修改

至少验证：
- 族
- 族类型
- 材质
- 视图
- 图纸名称
- 图纸编号
- 标高
- TextNote.Text
- 一个可写实例字符串参数
- 一个可写类型字符串参数
- 明细表列标题
- 修订说明/签发信息
- 房间/空间/面积编号
- 用户工作集（如可用）

要求：
- 修改真实写入 Revit；
- 改为纯英文后该条从当前中文结果移除；
- 重复名称/非法名称等失败路径必须回滚且窗口不崩；
- 可改名=false 的行不能进入编辑。

### D. 删除 / 清空

1. TextNote 删除：预检显示影响数量，确认后删除。
2. 参数文本值：操作后宿主元素仍存在，只清空字符串值。
3. 选择一个中/高风险可删除对象做测试副本：预检必须先运行，再出现二次确认。
4. 取消确认：模型零修改。
5. 制造一条删除失败项 + 一条成功项：失败项回滚，成功项仍提交，Document 最终 `IsModifiable=false`。
6. 批次完成后一次 Ctrl+Z 应恢复整个成功批次。

不要在正式模型测试高风险删除，使用临时/副本项目。

### E. 定位与 ID

- 勾选多个可定位结果 → “在 Revit 中选择”正确设置选择集。
- 复制 ID 输出去重后的 ID。
- 工作集等非 Element 对象不能定位时应给出清晰提示，不崩溃。

### F. 性能

在用户当前常用大项目执行一次扫描：
- 记录元素规模、族实例数、总结果数、扫描耗时；
- UI 不能长时间显示无反馈；
- 扫描后 Revit 可继续操作；
- 记录读取异常数量。

## 5. 跨版本最低门禁

- Revit 2021：窗口启动、扫描、至少一次普通重命名 + TextNote 删除/撤销。
- Revit 2024：完整 A–F。
- Revit 2025：窗口启动、扫描、至少一次重命名 + 删除/撤销，确认 .NET 8 路径无差异。
- Revit 2026：若已安装，做与 2025 相同最小冒烟；未安装写 NOT_TESTED。

## 6. 文档同步

若编译或实机发现问题，只定点修复真实问题，并同步：
- README.md
- PROJECT.md
- WORKLOG.md
- docs/HANDOFF_3.1.35.md
- docs/FEATURE_AUDIT_3.1.35.md
- docs/REVIT_2021_2027_COMPATIBILITY_3.1.35.md
- docs/CHINESE_CHECK_CLEANUP_3.1.35_20260807.md

## 7. 最终证据

交付：
- 最终完整源码 ZIP
- 构建日志/binlog
- DLL 与部署 SHA-256
- Revit Journal
- GUI 截图
- 测试结果 JSON/Markdown
- 本轮问题与修复记录
- 最终 GO / 条件 GO / NO-GO

## 8. 判定

- 源码静态通过但未真实编译：条件 GO。
- 编译通过但中文清理 GUI/写操作未实测：条件 GO。
- Revit 2024 完整专项 + 2021/2025 最小冒烟通过：该功能可 GO；项目总发布状态仍由其它未覆盖项决定。

## 8. 继承的“打印 PDF”未验收变化

当前 `CHINESE_CLEANUP` 基线同时包含上一轮新增的 `PrintManager`“打印 PDF（Revit 2021+）”。该功能在网页端只有源码/静态证据，尚无当前产品 DLL 的 Windows/Revit 证据。

因此本轮完成当前源码编译部署后，必须继续执行：

`docs/DEEPSEEK_CODEX_BATCH_PRINT_PDF_ACCEPTANCE_3.1.35_20260807.md`

至少在 Revit 2021、2024、2025 的实际已安装版本中按任务条件验证；若某年份未安装则明确 `NOT_TESTED`。PDF 驱动必须区分 `AUTO_SAVE_PASS / INTERACTIVE_ONLY / FAILED`，不得把弹出“另存为”的驱动写成无人值守通过。

最终判断必须分别列出：
- 中文检查与清理 GUI/写操作；
- 打印 PDF；
- 其它既有回归；

任一新增产品功能没有对应实机证据时，项目总体仍只能“条件 GO”。
