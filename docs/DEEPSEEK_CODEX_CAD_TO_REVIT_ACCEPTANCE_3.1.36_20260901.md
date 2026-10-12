# DeepSeek / Codex｜YangTools 3.1.36 CAD 转 Revit 图纸 Windows 验收任务

## 0. 角色和禁止事项

你只负责 Windows/Revit 真实编译、部署、运行测试以及只修真实 compiler/API/runtime 问题。

禁止：

- 不新增需求；
- 不改成另一套 UI/架构；
- 不恢复 AI/摸鱼助手；
- 不升级 MCP SDK；
- 不重构已验证线性布置/材质主链；
- 不推 Git；
- 不生成分享 installer/payload；
- 不用旧 DLL 冒充当前源码编译结果；
- 不只文字声称 PASS，必须返回原始日志、SHA、截图/结构化测试结果。

## A. 源码与静态门禁

1. 解压到全新空目录。
2. 计算输入源码 ZIP SHA-256。
3. 执行所有 `tools/Test-*.py`；任何失败先停止 Windows 编译并返回原始输出。
4. 确认版本 `3.1.36`，Ribbon 预期 **31**。
5. 确认新增文件：`Core/CadToRevit/*`、`CadToRevitWindow.xaml(.cs)`、`CadToRevitCommand.cs`、16/32 图标。

## B. 真实编译

1. 优先独立编译 Revit 2024、2025；每年使用对应 Revit API，禁止跨年复用 DLL。
2. 0 error 为硬门禁；warning 原样记录并判断是否与本功能相关。
3. 若机器同时有 2021–2026，继续逐年编译并记录；2027 如果缺 .NET 10，保留真实 `NETSDK1045`，状态写 NOT_BUILT，禁止伪造 PASS。
4. 返回每年 build log、DLL size、SHA-256。

## C. 部署/Ribbon/UI

1. 仅部署本次实际构建 DLL，记录构建 DLL 和部署 DLL SHA-256，必须一致。
2. Revit 2024/2025 启动无加载异常。
3. Ribbon 精确 **31**；`导入工具区 → CAD转Revit图纸` 存在，16/32 图标正常，无空白/拉伸/明显风格错位。
4. AI/摸鱼入口必须 0；MCP 状态仍正常。
5. 逐一切换四主题并打开映射窗口：
   - 暖光米白
   - 深色现代
   - 专业蓝灰
   - 苹果水滴
   检查标题、Tab、表格、ComboBox、按钮、滚动、禁用态均可读，无黑块/透明文字/控件裁切。

## D. 真实 YangCAD Exchange 输入（P0）

必须使用 YangCAD 当前 RVT Exchange/预处理功能实际生成的 JSON，不允许手工重写一份“刚好符合 Revit Reader”的 JSON 作为唯一证据。

1. 记录真实 schema/version/units/origin，以及 Text/Curve/Hatch/Dimension 的真实字段片段（脱敏即可）。
2. 如果 Reader 因字段别名不一致无法读取，只允许在 `CadRevitExchangeReader` 增加兼容别名/正确单位或原点处理；禁止重新设计协议。
3. 扫描摘要对象数量必须与真实 JSON 对得上；unsupported/坏对象必须明确 warning，不得静默消失。

## E. 坐标/比例硬门禁（P0）

选择真实图中至少 5 组已知点/长度，覆盖水平、垂直、斜线、文字位置和标注：

- 相对距离误差 ≤ 1 mm；
- 不允许全图统一倍率错误；
- 不允许 X/Y 对调、镜像、整体旋转错误；
- 如果 Exchange Origin 非零，验证扣除后的相对布局；
- 记录 CAD 原值、单位、理论 Revit 值、实际 Revit XYZ/长度、误差。

P0 坐标失败时停止后续业务验收，先修转换器。

## F. 文字 / 引线

至少准备：

- `RVT-T H100 romans.shx`，手工映射到 Revit `2.5mm`；
- 第二个不同 CAD 文字类型；
- 单行文字、多行文字、旋转文字；
- 无引线、有一条引线、带肘点引线（真实 Exchange 有数据才测）。

门禁：

1. 程序不能自动把 H100 当作 Revit 100mm/1mm/其它猜测高度。
2. 目标 TextNoteType 正确；内容、位置、旋转基本一致。
3. 引线跟随文字创建；失败不得导致 TextNote/Leader 半残留。
4. `跳过` 类型不创建且不计失败。

## G. 线条

1. `#0.25 Continuous White → #0.25 Continuous` 使用现有类型。
2. 一个不存在的 `#0.xx Continuous ...` 使用“自动新建”，验证名称归一、线宽和 Solid pattern。再用一个非 Continuous 且项目不存在同名 LinePattern 的样本，必须要求改用已有/复制/跳过，不能静默变成实线。
3. 另一个使用“复制现有”，必须复制用户指定模板；模板不存在必须失败，不允许静默拿第一个 LineStyle。
4. CAD炸开参照可手动选择，但初次扫描绝不能自动选中。
5. 覆盖 Line / Polyline / Arc / Circle。

## H. 填充

1. 现有 FilledRegionType 映射。
2. 复制现有类型。
3. 一个简单 Hatch 自动新建/匹配 FillPattern。
4. 一个不确定/复杂 Hatch 设“跳过”。
5. 边界闭合、位置、区域正确；失败不留下半个 FilledRegion。

## I. 标注（P0/P1）

1. 真实 CAD Linear Dimension 至少 3 个，包含水平/垂直/斜向可用样本。
2. 目标 DimensionType 可切换。
3. 每个标注的参考 DetailCurve 使用专用线样式 `YangTools-尺寸界线`，不能混用普通 `#0.25 Continuous` 等结构线。
4. Dimension 必须是真正 Revit `Dimension`，不能用 TextNote + 线假装。
5. 记录 Dimension 引用的 DetailCurve ElementId/Reference 是否有效。
6. 强制制造一个坏 Dimension（缺参考线或零长度）：该 Dimension 失败时不能留下孤儿尺寸界线。

## J. 映射持久化

1. 保存映射，关闭窗口，再打开同一 JSON；映射恢复。
2. 关闭 Revit/重新打开后映射仍恢复。
3. 换一个 RVT，目标按类型名重新查找，不依赖旧 ElementId。
4. `Mapping.json` 不得保存 Revit ElementId。

## K. 事务 / 撤销 / 混合失败

1. 一张同时含文字、线、填充、标注的真实图完成转换。
2. 混入至少一个故意失败对象：成功对象保留、失败项明确列出。
3. 全部映射设为跳过/制造零成功：整个操作不得留下类型或图元。
4. 正常有成功批次完成后只按一次 Ctrl+Z，必须撤销整次 CAD→Revit 创建，包括新建类型和尺寸参考线。
5. Undo 后文档可继续编辑，无活动事务。

## L. 回归

只做冒烟，不顺带改功能：

1. 线性布置 P0 原世界 Z 门禁：至少一组实例误差 ≤1 mm。
2. 简易绑定材质：一组现有项目 Material 成功 + Ctrl+Z。
3. 批量绑定材质：打开 UI/最小一组绑定。
4. Assistant 旧数据清理/MCP 数据保留不回退。

## M. 交付证据

返回：

- 最终完整源码 ZIP + SHA-256；
- 2024/2025 build log、DLL SHA、部署 SHA；
- 31 Ribbon + 四主题窗口截图；
- 真实 YangCAD JSON 样本字段摘要；
- 坐标硬门禁表；
- 文字/线/填充/标注逐项结果；
- Ctrl+Z、零成功回滚、失败标注无孤儿线证据；
- 修改文件清单和真实 compiler/runtime 修复说明。

最终只能在证据齐全后写 `WINDOWS/REVIT GO`。否则保持 `STATIC GO / WINDOWS REVIT RETEST REQUIRED`。
