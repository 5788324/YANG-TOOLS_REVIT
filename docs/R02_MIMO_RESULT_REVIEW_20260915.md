# R02 Mimo 结果独立复核｜2026-09-15

## 结论

**R02 CORE RUNTIME GO（针对修复后的 Runtime 候选）**。

- 原始静态源码 ZIP：`YangTools_Project_3.1.38_Source_REBAR_R02_STATIC_20260915.zip`
- 原始源码 SHA-256：`4CDD1C02DCAA6B6F030AB519714DCE1B4DB5F43444996387E818D876780B18CE`
- Mimo 结果包：`RESULT_YANGTOOLS_REBAR_R02_MIMO_20260915_1146.zip`
- 本次独立重算结果包 SHA-256：`EEEF6252D8AFDC863012181D464B5499755AC748DBD0EB7B88B1B754AFA2E8B5`

原始 R02 源码不能继续作为可编译候选，因为 Revit 2024 需要 1 处 API 签名修复：

```csharp
// 原源码
manager.SetPreferredConstraint(coverCandidate);

// Revit 2024 实际编译通过
manager.SetPreferredConstraintForHandle(planeHandle, coverCandidate);
```

因此后续源码必须继承这项修复，并通过兼容层隔离 2024 与 2025+ API 差异。

## 已核实的 Runtime 证据

1. Revit 2024 Fresh Build：0 error；修复后 DLL SHA：
   `E2C39053A13076D806E77BB835FD5159C1436177ABF760BF6F915E8CA5895DCA`。
2. `journal.0099.txt` / `journal.0100.txt` 可证明实际 Revit 2024.3 运行链，并记录从 2024 Addins 路径加载 YangTools。
3. Face probe：宿主 321969，6 个 exposed planar face，选中面 Cover=25 mm。
4. dryRun：模型钢筋 0→0，Expected centerline offset=31.35 mm。
5. Single：Id 321976，ShapeDriven / Standard / ToCover；actual=expected=31.35 mm。
6. Cover 25→50 mm 后：
   - Rebar Id 仍为 321976；
   - 未调用 YangTools 修改/重建；
   - 中心线距离 31.35→56.35 mm；
   - preferred constraint 仍为 ToCover。
7. NumberWithSpacing：Id 321978，6@200，ArrayLength=1000 mm，ToCover 可读。
8. 错误矩阵 7 项均明确失败，没有半成品。
9. Undo：专用创建 Id 321979，`PostCommand.Undo` 后钢筋数 3→2，321979 被撤销。

这组证据已经足够证明 R02 的核心假设：**Shape Driven Rebar 可以真正绑定 Host Cover，并在 Cover 改变后保持同一 ElementId 自动随约束更新。**

## 证据卫生问题

### 1. 截图标记错误

`screenshots/revit2024_r02.png` 实际标题栏显示 **Autodesk Revit 2025.4 - Bridge 1 RC.rvt**，不能作为 Revit 2024 UI 证据。

本次不因此否定核心 Runtime：Revit 2024 journal、MCP 原始数据和 2024 Addins 加载路径能够独立支撑核心结论。后续 Mimo 截图必须显示正确 Revit 2024 标题栏。

### 2. loaded_assembly MCP 探针失败

`mcp_raw/loaded_assembly.json` 返回“当前没有活动项目”，因此不能把它单独当成 Loaded SHA 证明。

但 `journal.0100.txt` 记录实际加载路径，`sha/sha_triple.txt` 对该路径计算 SHA，与 Build/Deploy 一致。故本轮接受三重 SHA，但在后续任务继续要求从运行中程序集路径取值并哈希。

### 3. Cover-follow 有中间失败探针

结果包中存在若干早期测量脚本得到错误距离或旧 Cover 值。这些不能被删掉，也不能当 PASS。最终采用的证据链必须同时满足：

`Cover=50` → `dryRun Expected=56.35` → `same Rebar Id=321976` → `measured=56.35` → `ToCover`。

最终链满足条件，因此 D 项 PASS。

## R02 的新架构发现

R02 的 NumberWithSpacing 只证明了 Layout API 可用，**不能证明这是生产意义上的“沿宿主表面布筋”**。

R02 创建直筋时把 `normal` 设为 Cover Face normal。对 Shape Driven set 而言，这会让钢筋集沿其 primary plane normal 分布，因此可能沿宿主厚度方向扩展，而不是沿面展开。

R03 必须修正为：

- 钢筋方向：`barDirection`
- Cover 法向：`coverNormal`
- Rebar primary-plane normal / set distribution direction：
  `barDirection × coverNormal`
- 这样分布方向位于 Cover 面内；
- 用 `Edge` handle 约束 Cover 深度；
- 用 `StartOfBar` / `EndOfBar` 约束两端边界。

## 未关闭项

- R02 UI 交互点击：NOT_TESTED，不阻塞 Core。
- 曲面/隐蔽面：NOT_TESTED，且不属于 R02 范围。
- Release：NO-GO。

下一阶段：R03 Surface Distribution + Varying Length。
