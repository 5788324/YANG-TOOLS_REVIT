# YangTools 4.0.0 RC2Y｜RC2X 第二批：族实例 Excel / 填充区域样式 / 导出备份结果

日期：2026-10-13
状态：SOURCE STATIC GO；**RELEASE NO-GO**。仅对有具体源码依据的旧缺陷进行修复，不以重写为目标。

## 输入约束
- 唯一输入：YangTools_Project_4.0.0_Source_RC2X_MCP_SHEET_FILLEDREGION_ATOMIC_STATIC_20261013.zip
- 输入 SHA-256：07D7638693A281E3FF66366F70588F54F2785BFCDDEFCE63741357787D64DBEA
- 保留 MCP 原 27 wire、Rebar、Ribbon、安装器、csproj、McpServer、此前 RC2X MCP Broker / Sheet Service；本批不推 Git，以 Google Drive 为主交接仓库。

## 1. 族实例管理：Excel 导入
旧流程在 UI 里使用 TransactionGroup+逐行 Transaction，单行失败可能留下其他行已提交；“成功/跳过”无法表达整批原子性。
新 FamilyInstanceExcelImportService：先对全部非空行做 ID、重复 ID、目标元素、参数稳定身份、重复映射、只读/不可编辑预检；预检失败不启动事务。忽略导出信息列“类型/标高/主体/固定”（保持旧语义），不存在或同名不唯一的参数直接拒绝，不能静默跳过。忽略空行与没有修改的输入，避免无意义 Undo。有效写入使用**一个事务**，任一 Set 失败全批回滚；回滚失败明确报告未知。提交后再定位参数并比较 String/Integer/Double 内部值与成功设置后的快照（Double 有容差）。Revit 数值单位解析仍以项目显示单位为准。
窗口只读 Excel/交互/呈现结果，源码服务不保留 WPF 引用；模型已提交但界面刷新失败时不谎报导入失败。

## 2. 项目资产：填充区域
新 ProjectAssetFilledRegionVisualService 同时接管前/背景图案、前/背景颜色及类型复制，代替三个事件中的直接事务。预检文档和类型身份、图案存在性、无变化不启动事务；名称复制时大小写不敏感避冲突，写入前读回、Commit 检查，失败 RollBack，提交后读回。UI 保留原按钮与 ComboBox 操作，补页面切换/窗口关闭后的过期刷新保护，提交成功与 UI 刷新失败分别反馈。
不触碰上一批已完成的填充区域名称/遮罩服务。其他线型复制、图纸管理、族删除仍留后续。

## 3. Batch 输出生命周期
BatchFileCommitService：NWC/IFC/DWG 输出成功但旧文件备份清理失败时，返回 BackupRetained+RecoveryMessages，BatchTaskEngine 将警告放入 result.Warnings，并准确告诉用户保留的目录。清理后的 DirectoryExists 检查抛错不允许进入普通回滚逻辑，以防误删已经提交的新输出。旧版本错误恢复过程本身未重写。

## 4. 验证边界
- 工作区全量 Python 静态回归：待最终记录。
- 新专项 Test-RC2Y-FamilyAssetBatch：75/75 源码约束通过，待 ZIP fresh 复测。
- 原 Test-ReviewFixes / Test-FamilyManagementV2 / Test-ProjectAssetRC2G 的少量断言迁移为验证新的 Service，而非旧 UI 事务写法。
- 43 XAML 可解析；不代表 WPF 正确渲染。
- 实际 Windows C# Build、Revit Loaded、不同项目单位的 Excel 数值行为、DLL 功能、Ctrl+Z、WPF DPI、备份占用/故障注入运行、Revit 2021–2027 兼容 **NOT_TESTED**。
- 4.0.0 RELEASE NO-GO；正式稳定仍为 3.1.36 FINAL_E2E。禁止把 Python 套件等同于 Revit 实测。

## 5. 下批优先级
1. Windows 编译和 Revit 2024/2025 代表性运行烟测（获取真实自动化执行环境，不要求用户手测）。
2. 族实例删除 trial-delete / 回滚 / 级联删除 / Undo 的独立服务与原子核验。
3. 图纸管理其余事务、线样式复制、批处理 PDF 替换和被占用文件处理、微工具 UI；逐个按证据修。
4. 对大窗口和高 DPI 做 GUI 级视觉验收；长期补全 MCP 对所有现有及新增功能的覆盖。
