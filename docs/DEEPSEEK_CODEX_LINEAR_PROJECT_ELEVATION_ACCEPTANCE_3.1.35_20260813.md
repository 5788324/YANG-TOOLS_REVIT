# DeepSeek / Codex：YangTools 3.1.35 线性布置 ProjectElevation 修复编译部署与验收任务

## 唯一基线

使用 `YangTools_Project_3.1.35_Source_LINEAR_ELEVATION_FIX_20260813.zip`。禁止从 20260812 DLL 或更旧源码补丁拼接。

## 原则

- 不新增功能，不推送 Git。
- 先真实编译，再部署，再用 Revit 自动化复验；不要让用户手工测试。
- 只修真实编译/实机错误。

## A. 编译

1. 解压到全新空目录。
2. 运行 `Verify-SourceBaseline.ps1` 与全部 Python 静态专项。
3. 至少构建 2024、2025；本机可用年份一并构建，禁止跨年份 DLL。
4. 记录 build/restore/binlog、DLL 大小与 SHA-256。
5. 部署后核对构建 DLL 与 `%APPDATA%\Autodesk\Revit\Addins\<年份>\YangTools\YangTools.Revit.dll` SHA-256 一致。

## B. 必测：复现用户原错误条件

优先在出现原错误的模型/坐标设置中测试。记录所选 Level：

- `Level.Elevation`（显示高程）
- `Level.ProjectElevation`（项目坐标高程）
- 两者差值
- 起点/终点相对高程
- 每个生成实例 `LocationPoint.Point.Z`

用户原错误回归目标：

- 旧错误特征：期望约 9000 mm，实际约 5500 mm，差约 3500 mm。
- 修复后必须按 `ProjectElevation + RelativeElevation` 得到正确世界 Z。
- 若 `Elevation - ProjectElevation ≈ 3500 mm`，最终实例不得再次出现 3500 mm 系统偏差。

每个实例最大 Z 误差 ≤ 1 mm，并核对 `LevelId` 仍为用户选择的 Level。

## C. 高程矩阵

至少覆盖：

1. `Elevation == ProjectElevation` 的普通项目标高；
2. `Elevation != ProjectElevation` 的共享坐标/高程基点项目；
3. 正相对高程；
4. 负相对高程；
5. 起点高、终点低；
6. 起点低、终点高；
7. 固定数量 5，检查中间线性插值；
8. 固定间距且末端不补，检查实际首尾分别精确等于起/终点；
9. 反转路线；
10. Ctrl+Z 一次撤销整个批次。

## D. 防回退

- 不允许把世界 Z 改回 `Level.Elevation + relative`。
- 不允许为绕过校验删除 `FamilyPlacementHelper` 的世界位置验证。
- 如果某 FamilyPlacementType 对 `LocationPoint` 有特殊语义，必须记录该族 PlacementType、类别、相关内置 offset 参数与实际几何 Z，再定点处理；禁止按参数中文名称 `LookupParameter(name)` 猜偏移。

## E. 证据

输出：

- 最终源码 ZIP；
- 编译日志/binlog；
- 部署 DLL SHA；
- Revit Journal / YangTools 日志；
- 每个测试实例的 JSON/CSV（Elevation、ProjectElevation、Relative、ExpectedZ、ActualZ、Error）；
- 原问题模型回归结论；
- README / PROJECT / WORKLOG / FEATURE_AUDIT / 兼容性 / HANDOFF 同步状态；
- GO / 条件 GO / NO-GO。
