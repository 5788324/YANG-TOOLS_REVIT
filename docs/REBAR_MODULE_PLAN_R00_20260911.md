# YangTools 钢筋模块规划基线 R00

日期：2026-09-11  
状态：**规划已锁定；进入 R00 能力探针开发**  
主测：Revit 2024  
目标兼容：Revit 2021–2027

## 1. 结论

钢筋模块不按“梁 / 柱 / 板 / 桥墩 / 箱梁”拆底层，而按 Revit 钢筋机制拆成两条核心引擎：

1. **Shape Driven Engine**：凡是可由 `RebarShape + Constraints + Layout` 稳定表达的情况优先使用。
2. **Free Form Engine**：处理沿复杂曲面、非平面路径、复杂变截面等 Shape Driven 不适合表达的情况。

这不是“简单 / 复杂”的机械二分。多平面 Shape、Varying Rebar Set 等仍可属于 Shape Driven；Free Form 也只有在使用有效的更新 Server / 约束体系时，才适合承担需要随宿主长期更新的钢筋。

`AreaReinforcement / PathReinforcement / RebarInSystem / Fabric` 不作为第三个核心引擎，但必须视为 **System Reinforcement Adapter**，后续单独接入；第一阶段不把它们混入普通 `Rebar` 的创建修改逻辑。

## 2. 对前期使用总结的校正

### 2.1 “钢筋是一条多段线”只是一种近似

更准确：钢筋中心线是 **Curve chain**，可由直线、圆弧和其他 Revit 支持的钢筋曲线组成，也可能是多平面 / Free Form。不能把底层模型固定成 Polyline。

### 2.2 “每一段对应一个宿主面 + 保护层”不完全成立

Shape Driven 的几何由多个 `RebarConstrainedHandle` 控制。约束目标可能是：

- Host Face / Host Cover；
- Other Rebar；
- Surface；
- 特定箍筋边、弯角或 Hook Bend 等。

因此底层对象应是 **Handle → Constraint Target**，不能硬编码成 `Segment -> Face`。

### 2.3 “创建第一根，然后排布成钢筋集”基本正确

钢筋集应优先使用 Revit 原生 Layout / Distribution，而不是逐根复制 Element。Shape Driven 和 Free Form 的排布 API 不完全相同，统一放在上层 Layout Intent，下层由各引擎实现。

### 2.4 “纵筋先、箍筋后”不能定成固定规则

Revit 的自动约束逻辑中，Standard 直筋会优先寻找穿过的 Stirrup/Tie 弯角作为约束候选。实际工作流可能是：

- 先建立箍筋骨架，再让纵筋贴合箍筋；
- 或先建立纵筋，再手动/规则化约束箍筋；
- 或双方都创建后做 Relationship Resolve。

因此模块不规定固定创建顺序，采用 **创建 → 关系解析（Resolve Relationships）** 两阶段。

### 2.5 “Free Form = 所有复杂情况”也不成立

只要 Shape Driven 能稳定表达，就优先 Shape Driven，因为 Shape、参数、Schedule、约束和后续修改通常更可控。Free Form 用于真正需要曲面 / 非平面 / 复杂路径驱动的场景。

## 3. 最终架构

```text
Rebar/
├─ Inspection/              # 只读事实探针（R00）
├─ Intent/                  # 与 UI/MCP 解耦的设计意图
├─ Geometry/                # 宿主面、路径、局部坐标、Reference 解析
├─ Engines/
│  ├─ ShapeDriven/
│  └─ FreeForm/
├─ Layout/                  # 数量、间距、Varying、区域规则
├─ Relationships/           # Cover / Rebar-to-Rebar / Stirrup / Hook / Coupler / Lap
├─ Modify/                  # 原位修改、重解约束、重建
├─ QA/                      # 碰撞、净距、Cover、规则检查
├─ Standards/               # BS8666_2005 / BS8666_2020 / Project Profile
├─ Schedule/                # Schedule Mark / Bar Mark / BBS
├─ Compatibility/           # Revit 2021–2027 API 能力矩阵
└─ SystemReinforcement/     # Area/Path/RebarInSystem，后续适配
```

## 4. 创建策略

默认 UI：

```text
创建方式
● 自动判断
○ Shape Driven
○ Free Form
```

自动判断原则：

1. 可由可复用 Shape、平面/多平面几何和 Revit Constraint 表达 → Shape Driven。
2. Shape Driven 可通过 Varying Length 处理的斜面/直线变截面 → 仍 Shape Driven。
3. 需要曲面相交、非平面路径、复杂变截面或 Surface/Aligned Free Form 分布 → Free Form。
4. 需要宿主自适应但 Free Form 无有效 Server/约束能力 → 不静默创建“弱约束”结果，预览中明确警告。

## 5. 修改策略

修改是本模块的主要难点，固定分三级：

### M1 参数原位修改

直径、Shape 参数、数量、间距、Layout 等可安全修改时，不重建。

### M2 约束重解 / Retarget

宿主尺寸、横坡、纵坡、保护层、目标钢筋变化时，尽量保留原 Rebar，重新解析 Constraint Target。

### M3 Rebuild

当拓扑真正变化（段数变化、Shape Driven ↔ Free Form、复杂 Crank/Hook 变化等）才重建。重建必须：

- 先读取 Intent / 元数据；
- 在同一 `TransactionGroup` 中创建新对象并验证；
- 成功后才删除旧对象；
- 任何失败整体回滚；
- 保留 Schedule Mark / 项目参数等需保留数据。

**注意：Snapshot 只用于审计/比较，不作为回滚机制。真正回滚只依赖 Revit Transaction / TransactionGroup。**

## 6. Managed Rebar 元数据

插件创建或正式接管的钢筋必须记录最小设计意图，建议用 Extensible Storage（必要时辅以可见共享参数）：

- schemaVersion；
- managed / unmanaged；
- intentId / groupId；
- engineMode；
- source Schedule/Bar Mark；
- ruleProfile；
- host UniqueId；
- geometry selection semantic hints；
- creation Revit version。

不能只持久化一个原始 Face `Reference`：宿主拓扑变化后 Reference 可能失效。需提供 `Reference Resolver`，使用 Host UniqueId + 几何特征/语义重新寻找候选面，再由验证器确认。

已有人工钢筋默认 `unmanaged`，可以诊断/检查，但不自动重建或接管。

## 7. 箍筋、Hook、Coupler、Lap

这些是独立 Relationship 层，不塞进 Shape 创建器。

重点：

- Standard ↔ Stirrup/Tie Rebar-to-Rebar 约束；
- Hook 起终端类型、方向、旋转/终止处理；
- Coupler；
- Lap / splice / stagger；
- Starter Bar；
- Crank。

版本专属能力必须经 Compatibility Adapter 使用；例如 Revit 2025 原生 Splice 只覆盖部分 Shape Driven 直线段场景，不能把它当通用搭接引擎。

## 8. 碰撞策略

不做“一键自动求全局最优钢筋排布”。分三级：

1. **Detect**：钢筋-钢筋、钢筋-Coupler、钢筋-预埋件/洞口/预应力等碰撞与净距不足。
2. **Suggest**：根据项目规则给出偏移、层级、Crank、拆分、改搭接位置等候选方案。
3. **Resolve**：只对明确、可验证的规则自动修改；其余要求人工确认。

性能采用局部 Broad Phase → 精确 Narrow Phase：先 BoundingBox / 空间索引过滤，再对候选做中心线+半径或必要的实体几何检查，禁止默认全模型两两 Solid 相交。

## 9. 标准与项目规则

BS 8666 不写死到几何引擎。规则采用 Profile：

```text
BS8666_2005
BS8666_2020
Project_WadiClimb
Custom
```

Rule Profile 负责 Shape Code、弯曲半径、端部、搭接/锚固、Schedule/BBS 检查；Rebar Engine 只负责 Revit 模型表达。

## 10. Revit 版本策略

- **Revit 2024**：第一开发和实机验收版本。
- **2021–2027**：保持编译兼容，但不假设各版本能力一致。
- 2025/2026/2027 新能力通过 Capability Matrix / Adapter 开启，旧版本走回退实现或明确“不支持”。
- 禁止在业务层散落 `#if REVITxxxx`；版本判断集中在 Compatibility。

## 11. 性能与安全基线

- 所有几何写操作必须在 Revit 主线程。
- 批量写支持 dryRun / preview。
- 所有正式写操作可单次 Ctrl+Z。
- 大模型默认限定选择集/区域，不默认全模型扫描。
- 缓存只缓存可安全复算的信息，Revit Element/Geometry 生命周期对象不跨文档/事务长期持有。
- QA 与 Inspector 默认只读。

## 12. 开发路线

### R00 — Capability Probe / Inspector（当前）

只读诊断真实钢筋，验证我们对 Revit API 的理解，不创建钢筋。

### R01 — Shape Driven Minimal Create

单根 + 标准 Layout，真实 Cover / Host Constraint。

### R02 — Shape Driven Modify / Constraints

参数修改、Constraint 查询/重定向、宿主尺寸/坡度变化回归。

### R03 — Varying / Multiplanar

直线变截面、横坡、纵坡、多平面 Shape。

### R04 — Free Form Aligned

沿路径/方向的 Free Form，明确 Server 与约束状态。

### R05 — Free Form Surface / Complex Path

曲面、复杂桥梁几何。

### R06 — Relationship

Stirrup/Tie、Rebar-to-Rebar、Hook、Coupler、Lap/Starter Bar。

### R07 — Collision / Clearance QA

先检测，后建议，再做有限自动修正。

### R08 — Production Tools

按面布筋、沿线布筋、区域变间距、扣除区、Starter Bar、搭接/锚固。

### R09 — Propagation / Similar Segment

相似节段传播与传播后 QA。

### R10 — Schedule / BS8666 / Drawing-Model QA

Schedule Mark / Bar Mark、BBS、BS8666 Profile、图模一致性检查。

## 13. R00 的硬验收问题

R00 不回答“能不能写”，只回答以下事实：

1. 选中对象到底是 Shape Driven 还是 Free Form？
2. 宿主是谁？Shape 是什么？直径是多少？
3. Layout / Distribution / Varying 状态是什么？
4. 第一/中间/最后一根长度是否发生变化？
5. 有多少约束 Handle？当前约束类型和目标是谁？
6. Standard 是否实际约束到 Stirrup/Tie？
7. Hook / Coupler / Lap 状态能否稳定读取？
8. Free Form 是否有有效 Server？约束是否可编辑？
9. 横坡、纵坡、变截面修改后上述关系是否仍保留？
10. 同一份数据能否从 UI 和 MCP 得到一致结果？

只有 R00 实机证据通过后，才进入 R01 创建器。
