# YangTools 4.0.0 RC2R｜模型修改区实体提取 Bug/UI 静态修复

- 上游基线：RC2Q，SHA-256 `D325195A7DB5E50BE499E9EC032BAA7C294F03AB6E2CBD9FF62891222C9044CB`
- 修改功能：`SolidGeneratorEngine`、`SolidGeneratorCommand`、`SolidGeneratorWindow.xaml(.cs)`；未改 `Rebar` / MCP wire / Ribbon / csproj / installer。
- 原 Bug：`ResolveTargetLevel` 用 `Level.Elevation` 而非 `ProjectElevation`；已指定标高失效后回退到最近楼层；实例仅校验族原点未校验完整 Solid；Material.Set 未核实返回值/读回；事务组失败回滚状态未校验；保存的标高不见时 UI 自动选首层；过长的源图元显示可能遮挡按钮；错误提示保证“已经回滚”过度断言。

## 修改方案

1. 全部原始非零体积 Solid 抽取后记录外包盒六边界与累计体积；转为族局部坐标后验证边界平移及体积不变；族放置实例后及放置 Transaction 提交后（但 TransactionGroup Assimilate 前）用同一几何路径验证世界几何。六边界容差 5 mm、累计体积容差 1%（并有极小绝对值地板），超差拒绝提交；为兼容 Revit Solid 合并，不强求放置后 Solid 数量与源几何一致。
2. 关联标高仅负责关联；相近标高使用 `ProjectElevation`，用户显式指定的失效 ID 不自动换楼层。Window 有已保存标高但项目找不到时保持未选择并提示。
3. 显式材质 `Parameter.Set` 返回 false 或 `AsElementId` 读回不一致时停止；不再静默宣称成功。材质留空时原默认材质行为不变。
4. 项目事务组 Start/Assimilate/RollBack 返回状态判定；回滚失败及状态不确定独立提示，不再对用户一律声称“已回滚”。
5. UI 标题与按钮保持中文/布局不重做；增加窗口最小宽度、长 ID 省略显示及完整 ToolTip，说明高度来自原模型几何；命令在实际引擎执行前确认当前 UI 文档未切换。

## 测试与发布门槛

本轮新增 `Test-ModelModifyRC2R.py`，覆盖原始 / 局部 / 放置几何边界、5 mm / 1% 数学边界、世界 Z 独立于关联标高、UI 失效标高及异常反馈等合同。完整静态回归需在原工作区及最终 ZIP 新解压目录分别执行，不得以 Python 字符串合同代替 C# 编译或 Revit 真实转换。

**运行限制：** 实测 `GetInstanceGeometry` 的坐标变换、多层 `GeometryInstance`、曲面/旋转 Solid 的包围盒细节、部分材质类别、不同模板的 FamilyPlacementType、多个 Solid 的布尔合并、5mm/1% false positive、族重新载入、真实 Ctrl+Z、Revit 2021–2027 仍是 NOT_TESTED。若几何核验无法证明等价，本功能倾向安全拒绝，不宣称支持全部构件。4.0.0 RELEASE NO-GO；稳定发布仍是 3.1.36 FINAL_E2E。

**下一轮 RC2S：** 模型修改区布尔几何操作，优先排查“保留刀具体”、顺序差集、输入对象被静默删除、UI 按钮/异常，以及 Revit 事务/Undo。
