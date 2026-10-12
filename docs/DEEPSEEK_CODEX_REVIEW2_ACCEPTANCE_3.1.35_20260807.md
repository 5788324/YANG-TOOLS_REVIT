# DeepSeek/Codex：YangTools 3.1.35 Review2 编译部署与复验任务

## 边界

- 使用本次 `Source_REVIEW2_20260807` 完整源码，解压到全新空目录。
- 不新增功能、不大重构、不推送 Git。
- 只修真实编译错误或以下实机步骤暴露的问题。
- 所有模型写操作只在测试项目/副本执行；每项验证 Ctrl+Z。

## A. 源码与编译

1. 运行 `Verify-SourceBaseline.cmd`。
2. 运行 `静态检查面板与编码.cmd`。
3. 分别构建 2021–2027：
   ```powershell
   dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c <年份> -p:Platform=x64 -bl:artifacts\build\<年份>.binlog
   ```
4. 每年保存 restore/build 完整日志、错误/警告数、DLL 大小与 SHA-256。
5. 缺 SDK、NuGet 或未安装 Revit 的年份标记“未完成”，禁止写 PASS。

## B. Revit 2024 批处理与云链接

1. 本地 RVT 重复链接：首次创建，第二次勾选“跳过已存在”必须跳过。
2. Desktop Connector 已同步 RVT：已有链接在 Revit 中显示 `Autodesk Docs://...` 时，第二次必须跳过；保存现有路径、候选路径和日志。
3. 相同文件名但不同项目/目录：不得误判为同一链接。
4. CAD 链接重复识别执行同样三组测试。
5. 人为制造链接失败，确认后续链接仍能继续，文档最终 `IsModifiable=false`。
6. 合并 PDF：
   - 已有旧 PDF + 导出成功：新文件替换，旧文件不残留；
   - 已有旧 PDF + 导出失败/目标占用：旧文件内容和 SHA-256 保持；
   - 不勾覆盖：明确跳过。
7. 外部模型缺少同名三维视图、视图集、DWG/PDF 设置：应明确报错且不回退当前视图。

## C. 模型修改区

1. 线性布置：2D/3D、水平/斜线、断开曲线，验证世界 Z、提示、回滚和撤销。
2. 基于面转换：混合成功/失败实例，失败项不阻断其它项，不留下半成品。
3. 标高修改：故意制造复制参数失败，确认不产生新旧重复实例。
4. 布尔几何：
   - 相交且可融合；
   - 不相交；
   - 相交但 Union 失败样本。第三种必须整体回滚，不能输出重叠实体。
5. Loft/实体提取：负/零/正目标标高，世界 MinZ/MaxZ 与源几何误差 ≤1 mm。

## D. 项目管理区

1. 族实例 Excel：一行成功、一行无效 ID、一行无参数/只读参数；成功行保留，失败行回滚，汇总正确，一次 Ctrl+Z 撤销全部成功行。
2. 族类型 Excel：包含改名和参数写入失败，验证同一行原子回滚。
3. 项目资产 Excel：本工具导出后直接导入；混合成功/失败行；Project Info 页必须禁止 ID 导入。
4. 图纸 Excel：新建、更新、空图号、无效参数、同号歧义；单行失败不回滚其它行。
5. 普通删除：族实例、资产、图纸均必须弹确认；选择“否”不修改模型。
6. 图框换型：快速连续选择两个类型，不得出现旧请求覆盖新请求；失败时 UI 与 Revit 实际类型一致。
7. 检查族类别修改：项目文档不应处于活动 Transaction 时执行 `LoadFamily`。

## E. 证据包

必须包含：

- 修改后的完整源码 ZIP；
- build/restore 日志和 binlog；
- 编译 DLL、部署 DLL 与逐文件 SHA-256；
- Revit Journal、YangTools 日志；
- 每项截图/录屏索引；
- 测试模型说明及前后数据；
- README、PROJECT、WORKLOG、功能矩阵、兼容性说明、HANDOFF 更新；
- `FINAL_VERDICT.md`，仅允许 GO / 条件 GO / NO-GO，逐项列出未测试范围。
