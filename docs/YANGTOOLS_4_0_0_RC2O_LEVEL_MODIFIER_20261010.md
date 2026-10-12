# YangTools 4.0.0 RC2O｜模型修改区：标高修改安全重构

日期：2026-10-10。上游 RC2N 唯一基线 SHA-256：`A0AA68AF5F415014F1687811047F9C7AFC888B826CF57B809B667B06B3A2952F`。

## 变更范围

- 新增 `Core/LevelModifierService.cs`：`LevelModifierRequest`、`LevelModifierResult` 和整批 `Execute`；全部目标在事务前预检，禁止跳过无效、只读、被固定/分组或无法安全处理的对象；有效变更在单个 Transaction 内执行，出现失败时尝试全部回滚；NoChange 无 Undo。
- `LevelModifierEngine.cs`：普通标高/Offset 和 MEP 起终点标高统一用 `Level.ProjectElevation` 计算项目世界高度；修改 Set 返回值校验，仍保持 MEP 两端独立高程。保留先前重建的子事务，但仅允许无宿主、非镜像/翻转、非结构、无父实例、OneLevelBased 点式族重建；返回真正新 ElementId。原始参数复制逻辑未重做，复杂族类型保持保守拒绝。
- `LevelModifierCommand.cs`：选择列表在模态对话框出现前冻结、目标通过 Request 创建、Service 负责事务；事务失败/未知回滚与已提交但回读或 UI 刷新失败清晰区分，不再部分成功后报告“操作成功”。
- 每个图元在事务内与 Commit 后都验证目标标高；LocationPoint/LocationCurve 的项目世界位置（1 mm），重建后的 Symbol 和旋转角度、MEP 双端 Level Id；若最终 Commit 后回读失败，明确报告“已提交但不确定”，不声称回滚。
- 无新 XAML、Ribbon、Rebar、MCP wire、csproj、安装器改动；未来 MCP 可通过非 WPF Request 复用，但**本批未新增 MCP 命令，也没有 MCP runtime 联调**。

## 已知限制

1. 宿主族、结构族、镜像/翻转、组合族，以及无可用标高/Offset 的复杂族**不会被强制删除重建**。预检遇到其中需要修改的图元会阻断整批；这是有意的安全限制，不代表完成支持。
2. 原有同族参数复制规则仍有跨类别或共享参数匹配风险；未取得 Revit 实测证据，不保证所有族参数复制完全无损。
3. 位置/标高的静态源代码合同无法替代实际 Revit 再生成后几何检查、宿主约束验证和 Ctrl+Z。
4. 此环境缺 Windows .NET SDK/Revit；Build、Loaded、真实模型、Undo 均 `NOT_TESTED`，**4.0.0 RELEASE NO-GO**。正式稳定发行版 3.1.36 FINAL_E2E。

## 测试范围

- 新增 `Test-ModelModifyRC2O.py` 静态代码合同与纯数学校验；不能称为 UI 点击/模型运行测试。
- 原 53 个 Python 测试套件要求全部保留、RC2O 增至 54 套；工作区与最终 ZIP 全新解压执行。
- `Test-ReliabilityHardening.py` F03 的旧 UI 内部 `TransactionHelper` 位置假设更新为检查新的 `LevelModifierService` 提交结果，原有提交判定语义保留。
- 原 `Test-ReviewFixes.py` 仍要求重建 SubTransaction；已保留该路径且增加安全限制。

## 后续

按照模型修改区优先计划，下一批 RC2P：`FaceBasedConverterWindow` 的基于面转换，重点宿主、世界位置、逐实例部分提交和删除重建的模型关系；其后 Loft、实体提取、布尔几何。不能直接授予 Windows/Revit GO；Git 操作仍由 Codex 执行；不要求用户手测。
