# YangTools 3.1.37｜从 CAD 粘贴：共享坐标定位

日期：2026-09-02

## 1. 目标

在现有 `导入工具区 → 从CAD粘贴` 的“定位方式”中增加第三项：

- 原点到原点
- 中心到中心
- **对齐共享坐标**

该功能只控制 Revit 导入放置方式，不创建、获取、发布或修改项目共享坐标。

## 2. Revit API 语义

新选项使用：

```csharp
DWGImportOptions.Placement = ImportPlacement.Shared;
```

Revit API 要求 Revit 文档存在可用共享坐标。YangTools 不做任何自动 fallback：如果 Shared 导入本身失败，明确报错并回滚，禁止悄悄改成 `Origin`。

需要特别区分“API 导入成功”和“工程坐标正确”：对于 CAD，Revit 在文件没有真正共享同一坐标系时，可能仍按 CAD WCS 原点与主体共享坐标关系放置，而不是一定抛错。因此 YangTools **不声称能够自动判断 DWG 与项目是不是同一工程坐标基准**；Shared 成功案例必须与 Revit 原生“By Shared Coordinates”或已知控制点/Transform 对比。

## 3. 代码变更

### `UI/PasteCadWindow.xaml`

- `CboPlacement` 新增“对齐共享坐标”；
- 新增 `TxtPlacementHint`；
- 定位选择变化时刷新说明。

### `UI/PasteCadWindow.xaml.cs`

- placement index 2 → `ImportPlacement.Shared`；
- UI 解释共享坐标前提；
- 原 Origin / Centered 行为不变。

### `Core/UserSettings.cs`

`PasteCadPlacementIndex` 合法范围由 `0..1` 扩为 `0..2`，保留用户上次选择。

### `Commands/PasteCadCommand.cs`

- 日志新增 Unit / Color / Placement；
- `DWGImportOptions` 显式 Dispose；
- Shared 导入异常给专用中文错误；
- 成功结果显示实际定位方式；
- 事务失败仍显式 RollBack；
- 不自动获取/发布共享坐标；
- 不允许 Shared 失败后静默 Origin。

## 4. 现有功能审查

当前主链设计继续保留：

1. 检测 AutoCAD `A$C*.dwg` 剪贴板临时文件；
2. 允许手动选择 DWG；
3. 复制到 YangTools 工作副本后导入，避免直接占用 AutoCAD 临时文件；
4. 工作副本在 finally 清理；
5. 支持单位、颜色、定位、仅当前视图；
6. 单个 Revit Transaction；
7. 成功后选中新 ImportInstance；
8. 一次 Ctrl+Z 撤销。

本轮未发现需要推翻现有主链的 P0 问题。

## 5. 静态门禁

新增：`tools/Test-PasteCad.py`

当前 19/19 PASS，覆盖：

- Shared UI；
- `ImportPlacement.Shared`；
- settings 持久化；
- 禁止 fallback；
- 日志；
- Dispose；
- RollBack；
- working copy cleanup；
- Ctrl+Z 提示。

全量 `tools/Test-*.py` 15/15 suites PASS；UI 静态：31 Ribbon / 0 errors / 0 warnings。

## 6. 实机验收门禁

必须在 Revit 2024 至少做：

### S1 原点到原点回归

原行为正常，位置与 3.1.36 一致。

### S2 中心到中心回归

原行为正常。

### S3 共享坐标成功案例

使用一个已建立共享坐标的 Revit 项目和对应坐标 DWG：

- 选择“对齐共享坐标”；
- 导入成功；
- ImportInstance Transform / 位置与手工 Revit “By Shared Coordinates”一致；
- 一次 Ctrl+Z 完整撤销。

### S4 Shared 拒绝/异常案例

构造 Revit 原生 Shared 导入会拒绝的条件（优先使用没有可用共享坐标的项目；不要假定“DWG 坐标语义不匹配”一定会抛错）：

- Revit 原生 API 若抛错/返回 false，YangTools 必须明确失败；
- 不得自动 Origin；
- 文档不留 ImportInstance；
- `doc.IsModifiable == false`。

### S5 坐标语义防误判

对一个坐标基准故意不匹配、但 Revit 仍允许 Shared 导入的 DWG：

- 允许 Revit 返回“导入成功”；
- 不能因此把“坐标正确”判 PASS；
- 必须读取 Transform/控制点并与已知正确结果比较；
- 报告明确区分 `ImportSucceeded` 与 `CoordinateAlignmentVerified`。

## 7. 当前状态

`STATIC GO / WINDOWS REVIT RETEST REQUIRED`
