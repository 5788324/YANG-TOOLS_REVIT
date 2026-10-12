# YangTools 3.1.38｜Rebar R00 Windows / Revit 2024 实机验收任务

## 目标

验证新增“钢筋诊断”只读能力探针在 Revit 2024 可编译、可加载、可读取真实 Rebar，并验证 MCP `inspect_rebar` 与 UI 使用同一事实数据。

本任务 **不得继续开发 R01**，不得修改现有可靠性加固主线行为，不得把本 R00 候选宣布为正式发布版。

## 输入

使用交付的：

`YangTools_Project_3.1.38_Source_REBAR_R00_STATIC_20260911.zip`

必须在全新目录解压，禁止覆盖旧源码目录。

## 1. 构建门禁

在 Windows 上执行：

```powershell
cd <源码目录>
dotnet restore .\YangTools.Revit.sln
dotnet build .\YangTools.Revit.sln -c 2024 -p:Platform=x64 --no-restore
```

要求：

- 0 error；
- 回传完整构建日志；
- 记录 DLL 路径、大小、SHA-256；
- 不接受只写“编译成功”。

如有余力再验证 2021 / 2025 / 2026 / 2027；R00 首轮硬门禁只要求 2024。

## 2. Ribbon / 只读门禁

Revit 2024 打开测试项目后：

- “检查工具区”扩展区域出现“钢筋诊断”；
- 选中钢筋可直接诊断；无预选时可框选/多选钢筋；
- 执行后模型 Dirty/Undo 栈不得新增 YangTools 写操作；
- JSON 输出到 `%LOCALAPPDATA%\YangTools\Diagnostics\Rebar\`。

## 3. 样本矩阵

至少准备/找到以下样本：

1. Shape Driven 单根直筋；
2. Shape Driven 标准钢筋集；
3. Shape Driven Varying Length set；
4. Multi-planar Shape（如项目有）；
5. Stirrup/Tie + Standard 纵筋，最好存在 Rebar-to-Rebar constraint；
6. Free Form Aligned 或 Surface rebar（项目有则必须测）；
7. 有 Hook 的钢筋；
8. 有 Coupler / Lap 的钢筋（项目有则测）。

对每个样本保存：

- Revit 截图；
- ElementId；
- R00 JSON 对应条目；
- 关键属性与 Revit Properties UI 是否一致。

## 4. 变化回归

至少做 3 组：

### A. 横坡/纵坡

改变宿主坡度后 Regenerate，检查：

- Rebar 是否仍存在；
- Constraint target 是否保持；
- 第一/中/末样本长度是否合理变化；
- JSON 变化前后 diff。

### B. 尺寸/变截面

修改宿主宽/高或斜边尺寸，检查 Varying set 是否继续有效。

### C. Stirrup / Standard 关系

修改箍筋或纵筋直径/位置，检查 `ToOtherRebar`（如存在）及目标元素是否保持。

## 5. Free Form 重点

记录：

- `FreeFormHasValidServer`；
- `FreeFormHasValidAlignedServer`；
- `FreeFormServerGuid`；
- `ConstraintsCanBeEdited`；
- constrained handle 数；
- 修改宿主后是否响应。

没有有效 Server 的 Free Form 必须单独标记，不能把“能显示”当成“能随宿主稳定更新”。

## 6. MCP

启动 YangTools MCP 网关后调用：

```text
inspect_rebar()
```

使用当前 Revit 选择集；再调用：

```text
inspect_rebar([ElementId...])
```

要求：

- 两种方式都返回 JSON；
- 结果与 UI 对同一钢筋的核心字段一致；
- MCP 不弹选择框；
- 空选择/无效 ID 返回明确错误，不修改模型。

## 7. 回传证据

必须回传：

- build log；
- DLL SHA-256；
- Revit 版本号；
- 样本截图；
- R00 JSON；
- MCP 原始返回；
- 变化前后 JSON diff；
- PASS / FAIL 明细。

最终判定只允许：

- `R00 RUNTIME GO`；
- `R00 CONDITIONAL GO`；
- `R00 NO-GO`。

不得把 R00 Runtime GO 扩大解释成整个钢筋模块 GO。
