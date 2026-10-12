# YangTools 4.0.0 RC2T｜模型修改区七项综合收口审查

日期：2026-10-12  |  基线：RC2S（SHA-256 `8B471ACC72D26AA7FC10CA6DAEF77B08BF62341DF4A0E4DA120D23C2DD288AD3`）

## 本批范围

- 横向审查七个模型修改命令、WPF 窗口、Core Engine、关联标高、外部事件、事务及用户可见状态。
- **本批修复的真实问题**：Loft 标高选择列表以共享/显示高程 `Elevation` 排序、已有目标标高 ID 失效时默认首层且仍可能通过名称回退；线性布置及沿线布柱旧保存族类型/标高失效时自动选择其他选项；沿线布柱实际 Base/Top+Offset 与插入点的项目坐标错误使用 `Level.Elevation`；路径拾取在 ExternalEvent 验证文档之前隐藏窗口，导致文档切换/事件拒绝时窗口可能一直隐藏；关闭按钮和 OnClosed 重复保存设置。
- **安全行为**：沿线布柱 Base/Top/插入点 Z 全部改用 `ProjectElevation`，提交后校验 Base/Top Level 及 Offset；ColumnAlongPath 的 UI `LevelOption` 字段明确为 `ProjectElevation`，只在原设置为空时默认选择。Loft 和实体提取都按 `ProjectElevation` 对齐；仅“此前从未选过目标标高”时允许默认首层；保存了旧 ID 时 ID 不能匹配就明确要求重选，禁止按相同名称偷换到新图元；线性布置已保存设置失效不自动选其他族或楼层，并让 `PathInfoText` 在窗口中实际显示；路径拾取进入已校验的回调后才隐藏，退出的 finally 恢复窗口。

## 七项模型功能源码级审查

| 模型修改功能 | 既有加固阶段 | RC2T 审查结论/仍需验证 |
|---|---|---|
| 沿线布柱 | RC2M + RC2T | 本次修复 Base/Top+Offset 实际项目坐标和提交后参数回读、保存选项失效保护；请求快照/数量上限/单事务已静态覆盖；曲线复杂输入与 Revit 放置待实机 |
| 线性布置 | RC2N + RC2T | 本次修 UI 隐藏/失效保存值/标高排序；非零项目基准、多段连续路径真实实例结果待实机 |
| 标高修改 | RC2O | 仍限制宿主和镜像构件重建；真实 Z/MEP 两端 Offset/Undo 待实机 |
| 基于面转换 | RC2P | 严格数据保留与整批回滚；复杂姿态、参数关联及宿主几何待实机 |
| Loft 实体生成 | RC2Q + RC2T | 本次修旧标高选择与排序；Source/Local/Placed 高度静态门禁、Void/复杂曲面待实机 |
| 实体提取 | RC2R | 多 Solid/材质和三阶段几何边界已静态约束，几何真实等价未证明 |
| 布尔几何 | RC2S | Void/刀具体/删除族范围与事务预检已有源码防护；族公式及级联依赖/真实布尔待实机 |

## 不扩大本轮范围

- 不新增功能按钮/参数设置或 MCP 路由；现存 MCP 27 wire 不能等同七项功能都已 MCP 可调用，需后续公共 Service 对接与真实 MCP 验收。
- 不改钢筋、Ribbon、安装器、版本构建配置或已通过独立实机验收的旧算法。
- 现有 `Level.ProjectElevation` 统一作项目坐标参考，`Level.Elevation` 仅可用于向用户显示共享/显示高程。依据 Autodesk Revit API 文档。
- 编译器环境中未检测到 Windows dotnet/Revit SDK 可运行构建。**静态代码词条或 XML 解析不能证明 C# 可编译，更不能证明 GUI、Undo、跨 2021–2027 兼容或项目链发布。**

## 验收门槛

- 源码工作区全量 Python 静态套件（预计 59 套）+ 最终 ZIP 全新解压独立复测；RC2T 专项 145 项静态合约；43 XAML 事件/资源静态校验；MCP Python 编译；ZIP CRC/中文文件名/源包卫生/差异核对。
- `SOURCE STATIC GO` 仅当以上均通过；`RELEASE NO-GO` 始终保持直至 Windows Revit 构建/加载、实际操作、读回和撤销提供原始证据。

## 后续

- **RC2U：P1 CAD 粘贴/CAD→Revit 与视图工具**的集成审查，重点选项、比例/坐标、层映射、静默跳过和 WPF/事务/Undo。按模块合并，不机械逐按钮拆批。
- 累积缺项：模型修改区 7 功能 MCP 对接率与实机验证；旧项目资产管理器 Fill/Copy/填充区域遗留；族实例管理器 Excel/删除；2021–2027 跨版门禁；UI 实机与安装器产品链。
- 不要求用户亲手测试；Windows/Revit 由可用独立执行器验收；不使用 Mimo、不由 ChatGPT 推 Git。
