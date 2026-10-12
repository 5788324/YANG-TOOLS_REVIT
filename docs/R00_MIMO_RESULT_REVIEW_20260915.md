# YangTools Rebar R00｜Mimo 实机结果复核

日期：2026-09-15  
最终复核：ChatGPT  
输入结果包：`RESULT_YANGTOOLS_REBAR_R00_MIMO_20260915_0935.zip`  
结果包 SHA-256：`756576782CBF3FD47B492DB1E79FCE7C1B37EFFF668556039CCCABA4DC1915F5`

## 1. 最终判定

### R00 总体

**R00 = CONDITIONAL GO（仅允许进入 R01 Shape Driven 最小创建器）**

更精确地拆分：

- Shape Driven 基础读取/单根/简单钢筋集：**GO**；
- R00 诊断服务 MCP 主线程链：**GO**；
- UI 实际点击路径：**NOT FULLY VERIFIED**；
- Varying / Free Form / Hook / Coupler / Lap / 坡度与尺寸回归：**NOT_TESTED**；
- YangTools 正式发布：**NO-GO**；
- 不允许把 R00 宣布为完整钢筋能力基线。

本结论的目的，是避免未测的高级能力阻塞 Shape Driven 基础工程继续开发。

## 2. 已核实通过

### 2.1 输入完整性

原始 R00 ZIP：

`YangTools_Project_3.1.38_Source_REBAR_R00_STATIC_20260911.zip`

本地重新计算 SHA-256：

`0E5A3353C56A9353D32899B964733995757ED367D67B7F396FF8415EA2DCF347`

与交接基线一致。

### 2.2 Revit 2024 构建

`build.log` 为 UTF-16，可正常解析。结尾明确为：

- 252 warnings；
- 0 errors；
- build elapsed 00:00:03.18。

但该构建并非原始 R00 源码原样构建，而是在工作树应用 2 个最小修复后构建。因此：

> 原始 R00 ZIP 只能算“源码输入完整”，不能算“原包原样可编译”。

两个修复：

1. `BatchTaskWindow.xaml.cs` 非法跨行插值字符串；
2. `RebarInspectionService.cs` Revit 2024 API 参数名 `suppressHooksAndCranks` 改为 `suppressHooks`。

### 2.3 Build / Deploy

2024 DLL：

`08EFB3D01B23A4B45A3D186FDDC06CA76C1CC914916567B23171F6813F242204`

结果包中 Build SHA 与 Deploy SHA 相同，**Build = Deploy PASS**。

### 2.4 Revit 2024 + MCP 运行链

原始 MCP 返回：

```json
{
  "ready": true,
  "revitVersion": "2024",
  "activeDocument": "Project1",
  "hasActiveDocument": true
}
```

`inspect_rebar` 返回的 `DocumentTitle=Project1`、`RevitVersion=2024` 与其一致。

因此可以确认：

- 2024 中存在可用 YangTools MCP 执行实例；
- R00 `inspect_rebar` 命令确实在 2024 主线程运行；
- 不是只拿静态 JSON 冒充运行证据。

### 2.5 E1 Shape Driven 单根

ElementId `322011` / `322012`：

- `EngineMode=ShapeDriven`；
- Host=`322004`；
- Bar diameter=12.7mm；
- Layout=`Single`；
- 中心线可读；
- 4/4 Constraint Handles 已约束；
- Target 为真实 Wall Host。

**E1 PASS。**

### 2.6 E2 Shape Driven Set

ElementId `322018`：

- `LayoutRule=NumberWithSpacing`；
- `NumberOfBarPositions=6`；
- `MaxSpacingMm=200`；
- `ArrayLengthMm=1000`；
- first/middle/last 三个 sample 均存在；
- 三个 sample length 均为 2600mm（Uniform，符合该样本）。

**E2 PASS。**

### 2.7 Inspector 只读性

前后 Rebar count：4 → 4。

R00 静态门禁也确认 Inspector 不包含：

- SetPreferredConstraint；
- SetLayoutAs*；
- SetCurves；
- DisconnectFromServer；
- Document.Delete；
- ElementTransformUtils。

**Inspector 本身只读证据成立。**

## 3. 必须降级的项目

### 3.1 E4 不是完整 PASS

结果中的 `322017` 有：

- Shape Driven；
- 7 条中心线；
- 7/7 Handles 约束到 Host Face。

但任务要求的核心还有：

- `ToOtherRebar`；
- target longitudinal Rebar；
- 修改纵筋/箍筋后再次读取关系。

这些没有执行。

而且 R00 Inspector 当时 `RebarStyle` 读取为空字符串，无法由 Inspector JSON 本身证明 `StirrupTie`。

因此最终判定：

**E4 = PARTIAL / PROBE PASS，不得写成完整 PASS。**

R01 基线已经补修 Inspector：

- Shape Driven：从 `RebarShape.RebarStyle` 读取；
- Free Form：从 `RebarFreeFormAccessor.RebarStyle` 读取；
- schema 从 `R00.1` 升到 `R00.2`。

### 3.2 Loaded SHA 未严格证明

任务要求：

`Build SHA = Deploy SHA = Loaded SHA`

结果包证明了：

`Build SHA = Deploy SHA`

但没有提供“已加载模块字节重新哈希”或等价的进程模块 SHA 证据。

当前只能结合：

- `.addin` 指向 deployed DLL；
- 2024 MCP 实例能执行 R00 新命令；

推断加载的是新部署版本。

这属于**强间接证据**，不是严格 Loaded SHA 门禁。

因此 Load Gate = **CONDITIONAL PASS**。

### 3.3 2024 UI / Ribbon 截图证据不合格

结果包中：

- `desktop_with_revit2024.png` 实际标题栏显示 `Autodesk Revit 2025.4 - Bridge 1 RC.rvt`；
- `revit2024_screen.png` 同样显示 Revit 2025.4；
- `revit2024_window.png` 基本为空白。

因此不能将这些截图作为：

- Revit 2024 Ribbon；
- “钢筋诊断”按钮；
- 2024 UI 点击成功；

的直接证据。

这不推翻 MCP 2024 运行证据，但 **UI click path = NOT_TESTED / evidence invalid**。

### 3.4 journal.0095 缺失

`RESULT.md` 和 `license_errors.txt` 引用了 `journal.0095`，并称其 2024 LicenseUpd 正常，但结果包中实际只包含 `0092/0093/0094`，没有 `0095`。

所以 0095 不能作为独立原始证据计入验收。

## 4. NOT_TESTED 保持原样

以下未测项没有发现伪造 PASS，应保持：

- E3 Varying Length；
- E5 Free Form；
- E6 Hook / Coupler / Lap；
- 横坡 / 纵坡；
- 宿主宽高/斜边尺寸变化；
- Stirrup/Standard 修改后关系保持；
- Revit 2025 runtime。

这些不阻止 R01 Shape Driven 最小创建器，但会在各自进入开发前重新设门禁。

## 5. R00 对后续架构给出的有效事实

现在可以把以下事实当作 R01 输入：

1. `Rebar.CreateFromCurves` 在 Revit 2024 可用于建立 Shape Driven 样本；
2. Shape Driven 单根可以获得真实 Host Face Constraints；
3. `RebarShapeDrivenAccessor.SetLayoutAsNumberWithSpacing` 路线有效；
4. 第一/中/末 Bar Position 可以读取中心线；
5. R00 Inspector + MCP 可以作为创建后的 readback 验证器；
6. Revit 2024 不应依赖 2025 新增的 `CanHaveVaryingLengthBars / HasVariableLengthBars` 作为唯一判据；R00 已用反射兼容为空。

## 6. 进入 R01 的边界

允许：

- Shape Driven 直筋；
- 有效 Rebar Host；
- 已有 RebarBarType；
- Single；
- NumberWithSpacing；
- dryRun；
- 一个 Transaction；
- 创建后调用 R00 Inspector readback；
- UI/MCP 共用 Service。

禁止扩展：

- Free Form；
- Varying；
- Hook；
- Coupler；
- Lap；
- Collision；
- BS8666；
- 桥梁自动配筋；
- 自动按面 Cover 求中心线。

最后一项留到 Shape Driven 约束阶段处理，避免 R01 一次承担几何语义和创建 API 两个风险。
