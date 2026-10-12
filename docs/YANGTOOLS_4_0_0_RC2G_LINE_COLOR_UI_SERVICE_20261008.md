# YangTools 4.0.0 RC2G｜线样式颜色安全编辑

日期 2026-10-08。范围仅 `ProjectAssetManagerWindow.LineStyleColor_Click`，不是整个资产管理器完成的宣告。

## 已修改

- 新增 `Core/ProjectAssets/ProjectAssetLineColorService.cs`：typed Request/Result，UI 只传线样式 CategoryId 和 RGB byte；Revit ExternalEvent 主线程执行。
- 颜色选择弹窗从 ExternalEvent 内移到原 WPF 点击入口；点击“取消”不排队执行，也不创建 Undo。
- 仍通过 `OST_Lines.SubCategories` 处理线样式（支持内置类别的负 ElementId）；不存在类别或只读文档明确报错。
- 新颜色与现有值相同不创建事务；有效变化只启动一个 Transaction，确认 Start/Commit，异常处理 RollBack，无法确认回滚状态不能谎称回滚成功。
- 排队操作使用所属 `_doc` 和冻结 RGB/ID；窗口关闭、标签变化、列表行失效时取消过期请求。模型确认提交后才更新 `ColorBrush`；区分模型失败和“模型已提交但 UI 刷新失败”。
- 旧线样式名称/线宽、线型图案 ComboBox、FilledRegion 色块/填充图案/遮罩路径保持原状（后者的 UI 选择提前改写 ViewModel 是未来检查事项）；全部 XAML、MCP、Rebar、Ribbon、csproj 未修改。

## 测试和限制

- 旧 45 个 suites 保留；`Test-ProjectAssetRC2F.py` 的“恰好 44 个旧套件”硬编码改为至少 44 个，以免未来增加测试产生假失败。
- 新增 `Test-ProjectAssetRC2G.py` 71 项静态源码/UI 合同；完整源树 + ZIP 干净解压分别 46/46 suites 通过；XAML XML 43/43 可解析。
- **无 Windows/.NET/Revit 实机环境**：C# 编译、ColorSelectionDialog 实际表现、颜色回读、撤销和界面点击结果均 NOT_TESTED。当前 4.0.0 `SOURCE STATIC GO / RELEASE NO-GO`；正式稳定版仍 3.1.36 FINAL_E2E。

## 下一步建议

RC2H 最多处理一条线型图案 ComboBox 的延迟提交安全路径，随后对 ProjectAssetManager 残余高风险点做一次 **收口审计**，将其余颜色、填充区域、复制/导出路径的未验证事项列为 backlog 并转入下一高耦合窗口，不把全项目 4.0 重构长期锁死在项目资产管理器。
