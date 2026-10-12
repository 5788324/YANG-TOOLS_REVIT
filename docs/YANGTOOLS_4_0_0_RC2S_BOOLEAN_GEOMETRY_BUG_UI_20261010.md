# YangTools 4.0.0 RC2S｜模型修改区布尔几何 Bug/UI 与安全收口

**基线：** RC2R `304C1D49426C29E0FDF845E88D6A8EF2E28BADFC118100556864C2657AFF219D`。**仅源码静态候选，不是 Build/Runtime GO。**

## 真实旧缺陷与处理

1. **差集失败但假成功**：旧版 `Difference` 失败会输出日志、把未剪切 Solid 继续加入；RC2S 必须先检查正体积交集，再执行 Difference；真正相交而运算失败一律抛错，使族事务退出并尝试回滚；无交集只是 no-op。融合先于剪切，连接保持独立实体语义；交集核查发生异常不能视作无交集。
2. **原模型删除静默失败**：`famDoc.Delete(GenericForm.Id)` 旧版吞异常继续；现先核验所有非空结果，再严格确认每个旧 Form 删除，异常触发族事务回滚。`GenericForm.IsSolid == false` 的 Void 拒绝以固定正实体替换；无有效几何的主体/目标拒绝，避免几何丢失。多 Solid 仍按现有算法处理，没有擅自加入新操作。
3. **删除刀具体/整个目标族**：目标图元或族定义删除失败不再只是 Debug 日志。删除族定义前先遍历项目内该族所有 FamilyInstance，存在未选中者就拒绝；同族主体禁止删定义。选中“删除目标族”隐含删除该族的实例，UI 文案明确。族定义还可能有非实例依赖，所以保留显著提示；实际级联影响需要独立 Revit 试删验收。
4. **事务与回读**：`EditFamily` 不能在项目可修改事务中调用。改为在无项目事务时打开并处理临时独立族文档，再在一个项目 `TransactionGroup` 内载入族/可选删除。Start/Commit/Assimilate 与 RollBack 状态均检查。`doc.Regenerate` 后确认主体图元、目标族、正体积几何以及所要求的目标删除；失败回滚或报告不确定。一个 Revit Undo 是设计目标，未实测。
5. **选择与 UI**：按 ESC 不再在真正选中对象之前清空旧选择；混入非族实例时保留原列表；项目切换停止；长实例 ID 改为计数概要 + ToolTip；窗口最小 620×425，文字说明融合→剪切→连接、刀具默认保留及危险删除族影响。

## 已知限制/风险（不夸大完成度）

- 主体族原生 GenericForm 全部转换为 FreeForm；尺寸约束、族公式、类型参数驱动几何可能失效。即使本轮已阻止 Void 和缺失 Solid，也无法保证复杂族所有参数化行为等价。**仍是破坏性操作，确认框提示先备份**。
- 即使删除族时项目没有未选中的 FamilyInstance，也可能有标签、明细表、族类型及其他依赖发生级联；需要 Revit `Document.Delete` 级联预演/实机验证，不能宣称零副作用。
- 差集完全切除主体导致空实体时保守拒绝；无交集允许目标保留并继续。融合几何自交、接触面边界、浮点容差和 Revit 2021–2027 的 FreeForm 执行仍未经过实机验证。
- 目标族重新载入只验证主体 Id/Family ID/存在正体积几何；尚未证明布尔输出与 Revit 运行实体体积/位置严格一致。未测试 Windows .NET build、Revit2024/2025 GUI、FamilyEdit/Load、Undo 与复杂族。
- 若事务组回滚返回未知状态，必须停止自动重试并查看模型。`FamilyFileHelper` 独立 GUID 临时目录照旧清理。
- 没有修改旧 27 条 MCP wire、Rebar、Ribbon、安装器或 csproj；没有新增专属 MCP 暴露，不应宣传 4.0 MCP 已覆盖全部 UI 功能。

## 验收/下一阶段

`Test-ModelModifyRC2S.py` 对业务合同、关键函数调用顺序和 XAML 事件绑定作源码级校验。工作区及最终 ZIP 新解压完整执行全部 `Test-*.py`；MCP `py_compile`、43 XAML 静态 UI、ZIP CRC/中文 installer 名校验。Windows/Revit/Undo 均 NOT_TESTED。4.0.0 RELEASE NO-GO；正式稳定仍是 3.1.36 FINAL_E2E。

下一阶段 RC2T：模型修改区阶段性总体审查（跨 7 项功能通用问题、静态编译风险优先排序），然后按 P1 CAD 粘贴/视图微工具继续；避免逐按钮重复重构。
