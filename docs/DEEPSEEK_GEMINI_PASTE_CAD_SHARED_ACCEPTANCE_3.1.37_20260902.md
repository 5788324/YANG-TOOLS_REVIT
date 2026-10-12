# Gemini / Windows 实机任务｜YangTools 3.1.37 从 CAD 粘贴共享坐标

## 身份

输入必须为 ChatGPT 生成的 3.1.37 静态候选。

当前身份：

`STATIC GO / WINDOWS REVIT RETEST REQUIRED`

Gemini 只能执行测试和回传证据，不自行宣布 FINAL GO。

## 1. 编译

至少编译：

- Revit 2024
- Revit 2025

记录：

- Build DLL SHA；
- Deploy DLL SHA；
- 0 errors；
- 版本 `3.1.37.0`。

## 2. Revit 2024 Runtime

完全关闭旧 Revit 2024，部署新 DLL，再启动新进程。

记录：

- Revit.exe Path / PID / StartTime；
- YangTools DLL path / SHA / Assembly Version。

## 3. UI

打开：

`导入工具区 → 从CAD粘贴`

确认“定位方式”精确包含：

1. 原点到原点
2. 中心到中心
3. 对齐共享坐标

共享坐标说明必须明确：

- 项目需已建立共享坐标；
- 不自动获取/发布；
- 不失败回退 Origin。

## 4. S1 Origin 回归

使用固定 DWG：

- Placement = Origin；
- 导入成功；
- 记录 ImportInstance ElementId / Transform；
- Ctrl+Z 删除。

## 5. S2 Centered 回归

- Placement = Centered；
- 导入成功；
- 记录 ElementId / Transform；
- Ctrl+Z 删除。

## 6. S3 Shared 成功 P0

准备：

- 一个已建立真实共享坐标的 Revit 2024 项目；
- 一个对应坐标 DWG。

先用 Revit 原生 UI “By Shared Coordinates”导入一次，读取并记录 Transform，随后 Undo。

再用 YangTools：

- Placement = 对齐共享坐标；
- 导入同一 DWG；
- 读取 ImportInstance ElementId；
- 读取 Transform / BoundingBox；
- 与原生 Revit UI 结果对比。

门禁：

- XYZ/Transform 在合理数值容差内一致；
- 不产生额外移动；
- 一次 Ctrl+Z 完整删除。

## 7. S4 Shared 拒绝/异常 P0

优先使用“Revit 原生 Shared 导入本身会拒绝”的测试条件，例如没有可用共享坐标的项目。不要把“DWG 与项目坐标语义不匹配”简单等同于一定抛错，因为 Revit 对 CAD 可能仍完成 Shared 放置。

若原生 API 抛错或返回 false：

- YangTools 必须明确失败；
- ImportInstance 新增数 = 0；
- 禁止自动改用 Origin；
- `doc.IsModifiable == false`。

另外增加一个“错坐标但 API 可能成功”的样本：

- 记录 `ImportSucceeded`；
- 读取 Transform/控制点；
- 与已知正确坐标结果比较；
- 报告必须把 `ImportSucceeded` 与 `CoordinateAlignmentVerified` 分开，不得把前者自动写成坐标 PASS。

## 8. 日志

检查 YangTools DiagnosticLog 包含：

- Unit
- Color
- Placement=Shared/Origin/Centered
- ThisViewOnly

## 9. Drive 回传

放入：

`03_Acceptance/20260902_PASTE_CAD_SHARED/`

至少：

- TASK
- BUILD
- DEPLOY HASH
- Revit 2024 process evidence
- Origin result
- Centered result
- Shared success result
- Shared failure result
- raw MCP/API response
- logs
- screenshots（若 AI 不能可靠截图，标 USER_TEST_REQUIRED）
- FINAL TEST EXECUTION REPORT

不得以数学模拟替代 Revit Transform readback。
