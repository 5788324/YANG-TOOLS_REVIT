# YangTools 3.1.35 线性布置 ProjectElevation 修复（2026-08-13）

## 用户实机证据

错误信息中的 Z：

- 期望：29.527559 ft ≈ 9000 mm
- 实际：18.044619 ft ≈ 5500 mm
- 差值：11.48294 ft ≈ 3500 mm

这说明 XY 正确，只有 Z 多计算了一个固定的 3500 mm 坐标基准差。

## 根因

上一轮使用：

`WorldZ = Level.Elevation + RelativeElevation`

但 Revit `Level.Elevation` 会受 Level 类型的 Elevation Base（Project / Shared）影响。XYZ 世界坐标属于项目内部坐标，应使用始终相对项目原点的 `Level.ProjectElevation`。

## 修复

当前固定为：

`WorldZ = Level.ProjectElevation + RelativeElevation`

中间实例仍按实际首尾实例的 `DistanceAlongPath` 比例线性插值；反转路线、保持垂直、一次 Ctrl+Z 等原规则不变。

UI 显示：

- 标高项目高程：`ProjectElevation`
- 若与 `Elevation` 不同，再显示“显示高程”

## 回归样例

- `Level.Elevation = 3500 mm`
- `Level.ProjectElevation = 0 mm`
- 相对高程 = `5500 mm`

正确世界 Z = `0 + 5500 = 5500 mm`。
旧公式错误得到 `3500 + 5500 = 9000 mm`。

## 当前状态

- UI 静态：0 错误、0 警告
- 线性高程专项：26/26
- 非零标高数学组合：20/20
- 3500 mm 坐标基准差真实错误回归：PASS
- Windows/Revit 当前源码：待重新编译部署与实机复验
