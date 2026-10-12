# YangTools 4.0.0 RC2Q｜Loft 生成世界 Z 与高度 / Bug / UI 审查

日期：2026-10-10；基线 RC2P；**仅源码静态候选，Windows/Revit Build/Loaded/GUI/Undo NOT_TESTED**。

## 源码确认的问题及已实施的修改

1. **标高关系与原几何高度**：本窗口实际上只有“目标标高”（用于放置族实例的关联标高）；没有独立的 Base/Top Level + Offset 输入。轮廓实体的上下界来自选中族实例的世界坐标。对已有流程不擅自增加双标高输入，避免重复叠加标高偏移。
2. **BUG-P0：未验证实际几何高度**：原代码仅校验族实例 LocationPoint，无法发现 Solid 本身 Z 下界/上界被不正确处理。现在逐条读取轮廓曲线 Tessellate 点计算 Z 范围、构造局部 Solid 后校验局部上下界，并在新族实例事务提交前读取项目内实际正体积 Solid 的世界 Z 上下界校验。使用 Solid.GetBoundingBox.Transform 八角变换，支持嵌套 GeometryInstance.GetInstanceGeometry 的项目世界坐标；误差允许 5mm，以规避弧线离散采样近似，但仍可能保守拒绝复杂 Loft。这是**静态实施**，并非 Revit 几何已实测。
3. **BUG-P1：目标标高选取基准错误**：无显式标高时旧代码按 Level.Elevation 匹配世界 Z；更正为 Level.ProjectElevation。显式指定的目标 LevelId 失效则报错，不再悄悄改到最近楼层。
4. **BUG-P1：输入重复/失效**：拒绝轮廓来自另一文档、重复选择和整体 Z 高度近零的无效轮廓。原轮廓闭合/自由曲线构建逻辑保留。
5. **事务结果**：保留一个 TransactionGroup 下“Load Family + Place Instance”两个子 Transaction；事务组 Assimilate 前必须通过几何回读，失败尝试整组 RollBack。回滚状态未知时明确提示，避免声称已回滚。
6. **UI**：长实例 ID 不再占满窗口，改为显示选择数量/顺序，完整 ID 放 ToolTip；设定 MinWidth；文案说明只有关联标高，实体高度由源世界坐标确定，不调整 Base/Top Offset；失败提示不再无条件声称“已回滚”。

## 未关闭风险 / 运行验证要求

- 跨 Revit 2021–2027 的 `Solid.GetBoundingBox()`、`GetInstanceGeometry()` 等 API / C# 编译须在 Windows 真正执行；当前无 C# 编译证据。
- 曲线 Tessellate 近似和复杂椭圆/扭转 Loft 的 BoundingBox 可能保守拒绝，5mm 不代表任何精度保证。必须以测试模型复核实体高度和项目世界 Z 范围（含共享标高、非零 Offset）。
- Void 模式可能不存在可读的项目正体积 Solid：当前仅校验源曲线与族局部 Solid 高度，项目 Void 正确性 NOT_TESTED，不能宣称通过。
- 族 SaveAs/同名族 LoadFamily、真正几何放置和单次 Undo 均需 Revit 实机验收；若加载前事务已提交而 UI 抛错，日志必须明确状态。
- 未加入 Base/Top Offset 表单字段，没有完成相关新需求；本轮解决的是“保留原轮廓世界高度”的原有功能语义。

## 固定范围

仅 `EntityGeneratorEngine` / `EntityGeneratorCommand` / `EntityGeneratorWindow.xaml(.cs)`，专项静态测试及文档；不动实体提取/布尔几何、Rebar、MCP 27 路由、Ribbon、installer、csproj。**项目 4.0.0 RELEASE NO-GO**；正式稳定版 **3.1.36 FINAL_E2E**。下一 RC2R：模型修改区“实体提取” Bug/UI/坐标与几何事务安全。
