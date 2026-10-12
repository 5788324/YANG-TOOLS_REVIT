# YangTools 3.1.36｜Revit 2021–2027 兼容性说明

## 当前结论

3.1.36 新增 `CAD转Revit图纸`，因此 3.1.35 的编译/运行证据不能自动继承为当前产品源码证据。

| Revit | 3.1.36 当前状态 | 真实证据 |
|---|---|---|
| 2021 | NOT_TESTED (Protected) | 真实编译 0 errors (DLL SHA: F3658E2F...)；PID 18544 保护运行 |
| 2022 | BUILD PASS / NOT_INSTALLED | 真实编译 0 errors (DLL SHA: 27C70646...) |
| 2023 | BUILD PASS / NOT_INSTALLED | 真实编译 0 errors (DLL SHA: 3B7E2C6D...) |
| 2024 | **PASS** | 真实编译 0 errors (DLL SHA: FA7F58D1...)；Live MCP/Revit API 全量通过 |
| 2025 | **BUILD PASS / DEPLOYED** | 真实编译 0 errors (DLL SHA: 23EB78BB...)；已部署至目标 Addins |
| 2026 | BUILD PASS / NOT_INSTALLED | 真实编译 0 errors (DLL SHA: B9F0A66F...) |
| 2027 | NOT_BUILT (NETSDK1045) | 真实缺少 .NET 10 SDK，明确记录 NOT_BUILT |

## 兼容策略

- 各年份独立编译，禁止跨年复用 DLL。
- 新功能使用 Revit 原生 `TextNote`、`DetailCurve`、`FilledRegion`、`Dimension`、`GraphicsStyle/Category` API。
- 不使用第二个 WPF Application；窗口复用现有主题体系。
- 不引入新的第三方运行时依赖。
- 不升级 MCP。
- 2024/2025 先作为功能实机主测；通过后再扩全年份编译矩阵。

## 3.1.36 特有实机风险

1. TextNote Leader API 行为；
2. DetailCurve 的 GeometryCurve.Reference 是否可稳定用于 NewDimension；
3. FilledRegion/Create 和类型复制在各年 API 行为；
4. GraphicsStyle/Category 创建线样式、LinePattern/LineWeight；
5. YangCAD Exchange 的单位/Origin/字段与实际 Reader 是否一致；
6. 一次 Ctrl+Z 的 TransactionGroup 合并。

在 2024/2025 实机 PASS 前，不得把 3.1.36 标记为 RELEASE GO。
