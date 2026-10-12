# YangTools 3.1.37｜Revit 2021–2027 兼容性说明

## 当前结论

本轮新增 `ImportPlacement.Shared` 到“从 CAD 粘贴”。`ImportPlacement.Shared` 是长期存在的 Revit API 枚举值；但源码兼容不等于各年份实机兼容。

| Revit | 当前 3.1.37 状态 |
|---|---|
| 2021 | NOT_BUILT / NOT_RETESTED 本轮 |
| 2022 | NOT_BUILT / NOT_RETESTED 本轮 |
| 2023 | NOT_BUILT / NOT_RETESTED 本轮 |
| 2024 | STATIC GO / BUILD+RUNTIME RETEST REQUIRED |
| 2025 | STATIC GO / BUILD RETEST REQUIRED；Runtime 不自动继承 |
| 2026 | NOT_BUILT 本轮 |
| 2027 | NOT_BUILT；仍受 .NET 10 SDK 环境门禁 |

## 共享坐标特有风险

1. Revit 项目必须存在可用共享坐标；
2. DWG 坐标基准必须与项目一致；但 Revit 对 CAD 的 Shared 定位可能在“并非同一工程坐标基准”时仍完成导入，因此“导入成功”不能替代控制点/Transform 对账；
3. Revit API 真正失败时不得静默 fallback 为 Origin；
4. `ThisViewOnly`、ViewType 与 Shared 的组合需实机；
5. 成功后应与 Revit 手工“By Shared Coordinates”结果对比；
6. 一次 Ctrl+Z 应完整删除新 ImportInstance。

## 兼容策略

- 各年份独立编译，禁止跨年 DLL；
- 不修改/获取/发布项目坐标；
- 不引入新第三方依赖；
- 不修改 MCP；
- 先以 Revit 2024 做 P0 实机。
