# YangTools 4.0.0 RC2Z｜图框 Excel、级联删除与 PDF 覆盖保护

日期：2026-10-13。唯一输入 RC2Y 源码 ZIP SHA-256：`DD915E30937A8DF7947093313B1B4F001DC71DAFCB9CF222DA8CA084FEC62634`。

## 目的及边界
用户只提供需求；开发、代码、验收、交接由 ChatGPT 负责。Google Drive 主仓库，禁止无必要的逐批 Git 推送。
不扩展功能、不变更历史 MCP 27 wire、Rebar/Ribbon/installer/csproj；不以静态通过替代 Revit 实机。

## 本批变更
1. **图框参数 Excel**：原 UI 每行静默忽略失效参数、仍然报告导入已提交。增加 `Core/Sheets/TitleblockExcelImportService`，拒绝指向非 TitleBlock 的图元 ID，筛去导出元数据“图纸号/图纸名/图框类型”，复用 RC2Y `FamilyInstanceExcelImportService` 做全表预检、稳定参数身份、一次事务、失败回滚、提交后内部值读回；UI 区分未变化、实际修改与刷新失败。
2. **级联删除**：`FamilyInstanceManagerWindow`、`SheetManagerWindow` 增加绑定文档/ID 有效性验证、Trial Delete 集合完整记录和执行阶段的集合一致性验证；额外关联图元变化则在真实事务 Commit 之前拒绝/回滚；提交后、事务组合并前核对选中图元已消失。图纸删除取消空泛的自动成功提示。
3. **图纸旧事务**：图号图名、单格参数、附加修订、图框单格参数四类入口补 Start/Commit 检查；图号图名和附加修订提交后检查模型实际结果。
4. **PDF 文件覆盖**：`BatchFileCommitService.CommitPdf` 对原 PDF 先备份、暂存文件再提交；支持不允许覆盖时拒绝；异常保留原文件备份与暂存文件，不做风险不明的删除；成功后清理失败单独发告警；批处理原生 PDF 与虚拟打印 PDF 都使用此路径。
5. **故障验证**：在 `tools/BatchFileCommitFaultHarness/Program.cs` 增加 5 种 PDF 故障注入用例，并新增 `Test-RC2Z-DeleteTitleblockPdf.py` 对核心源码路径作静态约束检查。

## 已知限制/待测
- 当前仅 Linux/Python 静态环境。不能宣称 C# 编译、WPF 布局、Revit 2021–2027 兼容、真实删除级联/宿主关系、Excel 写值准确或 Ctrl+Z PASS。
- .NET 故障注入 harness 仅编写，等待 Windows .NET 8 运行验证；其用例没有被计入 Python 套件运行通过数。
- 并非所有 SheetManager 旧代码都已重构（例如图框批量换型、旧部分辅助操作）；微工具 UI 也没有在本批大幅重做。
- Document.Delete 可能受 Revit 失败处理器影响，预检/实删集合一致性和日志仅为额外防线，不能代替真实模型和 Undo 验收。

## 放行
SOURCE STATIC GO 仅限源代码静态检查；4.0.0 RELEASE NO-GO；正式稳定版本仍为 3.1.36 FINAL_E2E。
