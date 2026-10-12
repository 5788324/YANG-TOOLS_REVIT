# R03 Mimo 结果复核｜2026-09-15

## 最终判定

**R03 CORE RUNTIME GO / UNIFORM POSITION READBACK OPEN / UI_INTERACTIVE NOT_TESTED / RELEASE NO-GO**

R03 已足够证明 Shape Driven 在 Revit 2024 中能够：
- 使用同一宿主的 exposed PlanarFace 建立 Edge / Start / End -> ToCover；
- 在斜平面保持正确保护层；
- 使用约束驱动 Varying 产生真实不同长度的同一 Rebar Set；
- 一次 Revit Undo 撤销最后一次产品创建；
- 2024 / 2025 两套 API 分支均可编译。

## 已独立确认的核心证据

- 输入源码 SHA：`A01E4E486A450DEFD2C1EFC6FEE21853CBB3FA8CB2B60D89157757FF8CFDD5C0`。
- `diffs/README.txt`：`NO_SOURCE_DIFF`。
- Revit 2024 Build：0 error / 258 warning。
- Revit 2025 compile-only：0 error / 293 warning。
- Build = Deploy = Loaded（2024）：`6B68393BED203B89270847E89D347A138AE849A11370CAB4F23762FC52A96EA9`。
- Journal 明确来自 Revit 2024.3.x（`C:\Program Files\Autodesk\Revit 2024` / `Release: 2024.3.4`）。
- R03-C：同一 set 的 sample length 为约 `3615.3 / 3815.3 / 3948.6 mm`，跨度约 `333.3 mm`，满足 true-varying 门禁。
- R03-D：斜面 cover 法向约 `(0.316,0,0.949)`，实际/期望中心线保护层偏移均 `31.35 mm`。
- Undo：最后创建 set 从集合中消失，4 -> 3。

## 需要纠正/保留开放的问题

### 1. 截图不是 Revit 2024

`revit2024_r03.png` 标题栏实际显示 **Autodesk Revit 2025.4 - Bridge 1 RC.rvt**，因此该截图不能作为 Revit 2024 UI 证据。

但 journal + MCP / loaded path 的 2024 证据独立成立，所以这不会推翻 R03 Core Runtime GO。

### 2. R03-B Uniform 的“中点重合”不能直接解释为钢筋集没展开

R03 结果用 `GetCenterlineCurves(..., barPositionIndex)` 做位置测量。Autodesk API 的语义是：对于非 varying set，各 index 的**形状**相同；钢筋在 set 中的实际位置还涉及 bar-position transform / moved-bar transform。真正用于“最终位置”读回的 API 是 `GetTransformedCenterlineCurves`。

因此 R03-B 的 identical midpoint 更可能是**探针几何读回方法不足**，而不是已经证明产品 set 真的堆在同一位置。

R04 将把 R00 Inspector 升级到 `R00.3`，优先读取 transformed centerline，并专门补测 uniform set 实际展开位置。

### 3. Floor 不是当前模板的有效 Rebar Host

这是测试模板/类型能力事实，不等价于“Revit Floor 永远不能作为钢筋宿主”。R03 采用有效的梯形混凝土墙完成同等几何门禁是可接受的。

## 进入 R04 的依据

R00-R03 已经建立：
1. Shape Driven 创建；
2. PlanarFace + Cover + ToCover；
3. 沿面 primary/distribution 方向；
4. true varying；
5. 斜平面；
6. Undo；
7. 2024/2025 Rebar API compatibility。

下一阶段优先响应项目最初确认的难点：**修改比创建更复杂**。因此 R04 不立即跳 Free Form，而先做 Shape Driven 的 M1 原位修改安全层。
