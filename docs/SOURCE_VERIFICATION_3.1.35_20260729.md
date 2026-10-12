# YangTools 3.1.35 源码核验与 Windows 编译准备报告

核验日期：2026-07-29  
核验范围：`YangTools_Project_3.1.35_Source_VERIFIED_20260729`  
结论口径：本报告只证明源码与构建配置达到“可交给 Windows 真实编译”的状态，不替代 .NET/MSBuild 编译和 Revit 实机验收。

## 1. 本轮范围

- 不新增 Ribbon 功能，不修改业务需求。
- 不推送 Git。
- 核验项目版本、多年份 Configuration、目标框架、条件编译、构建/部署脚本、安装清单和源代码安全约束。
- 运行当前环境可执行的 XAML/XML、主题、事件、UTF-8、Ribbon、AI/WPF 隔离、重复项目参数清理链路静态检查。
- 生成 DeepSeek/Codex 可直接执行的 Windows 编译、部署和 Revit 验收任务。

## 2. 源码与构建配置核验

| 项目 | 核验结果 | 说明 |
|---|---|---|
| 项目版本 | 通过 | `YangTools.Revit.csproj` 唯一 `Version` 为 `3.1.35` |
| Ribbon 基线 | 通过 | 28 个 `[RibbonButton]` 入口 |
| C# / XAML / 主题 | 通过 | 144 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题 |
| Revit Configuration | 通过 | 2021、2022、2023、2024、2025、2026、2027 七个独立配置 |
| 目标框架 | 通过 | 2021–2024=`net48`；2025–2026=`net8.0-windows`；2027=`net10.0-windows` |
| Revit API 包 | 通过（配置层） | `Nice3point.Revit.Api.RevitAPI/RevitAPIUI` 使用 `$(Configuration).*`，需 Windows/NuGet 真实还原确认具体包版本 |
| 平台 | 通过 | `x64`，输出路径按年份隔离为 `bin\x64\{年份}` |
| 跨年份 DLL | 通过 | 构建、安装和文档均禁止复用其他年份 DLL |
| 默认部署行为 | 通过 | 普通构建显式使用 `DeployAfterBuild=false`；只有带部署参数时写入用户 Addins 目录 |
| Addin 清单 | 通过 | `Assembly` 指向 `YangTools/YangTools.Revit.dll` |
| 源码纯净度 | 通过 | 源码包内未发现 DLL、PDB、EXE、NuGet 包或 RevitAPI 二进制 |
| 废弃入口 | 通过 | HelloWorld、MCP 状态旧入口、RibbonSettings、旧 AI 窗口及已删除 MEP 功能均不在源码中 |
| WPF 宿主隔离 | 通过（静态） | 未创建第二个 WPF `Application`；主题限定在 YangTools 根元素，不写 Revit 全局资源 |
| 非模态 Revit API | 通过（静态） | 项目管理窗口使用 `ExternalEvent`；重复参数清理未用 `Task.Run` 访问模型 |
| 重复参数清理身份规则 | 通过（静态） | 清理主线按参数 ID/GUID/Definition 严格识别，未使用 `LookupParameter(name)` |
| 数据完整性保护 | 通过（静态/模拟） | 内部值复制、未知级联回滚、值完整性与唯一性复核契约存在 |

## 3. 本环境执行结果

- 当前容器没有 Windows、PowerShell、.NET SDK、MSBuild、Revit 和 Autodesk 导出器。
- 因此没有执行真实 C# 编译、DLL 加载、部署、Revit Journal 或模型事务。
- 已执行：
  - `python3 tools/Test-UiPanelsStatic.py --root .`
  - `python3 tools/Test-ParameterCleanupPipeline.py`
  - Python 工具语法检查
  - 项目/解决方案/构建脚本/清单/条件编译静态核验

最终计数和结果在打包前复跑后写入本报告“最终静态结果”。

## 4. 本轮代码变更

- 产品业务代码：**0 个文件修改**。
- C#、XAML、主题、图标、安装逻辑：**未修改**。
- 仅新增/更新源码核验报告、Windows 执行任务、README、PROJECT、WORKLOG、功能矩阵、兼容性说明和交接文档。
- 修复一个确定的交付文档问题：3.1.35 重复参数清理说明内容被错误保存在 `...3.1.34.md`，已更名为 `...3.1.35.md`，README 链接恢复有效。
- 删除一个确定的包装冗余：字面文件名 `MicroTool_Developer_Guide%20V3.md` 与空格文件名版本内容完全相同，未被构建引用；保留唯一正式文件。
- 原因：当前未获得真实编译或 Revit 实机错误，不做猜测性修复。

## 5. 未解决风险

1. 七个 Configuration 尚未在 Windows 真实还原和编译。
2. `2027/net10.0-windows` 依赖可用的 .NET 10 SDK 和对应 Revit 2027 API NuGet 包。
3. Revit 2024 尚未逐个测试 28 个 Ribbon 入口。
4. 临时插件箱、文本等距分布、文本基准对齐、中文检查、系统面板设置、重复项目参数清理仍缺少 3.1.35 正式 DLL 的专项实机证据。
5. 参数清理涉及真实模型数据，必须在副本中验证值完整性、唯一性、未知级联回滚和一次撤销恢复。
6. AI DockablePane、主题切换和 ACC 云模型启动必须在 Revit 宿主中回归。

## 6. 当前判断

**条件 GO：可交给 Windows 编译与 Revit 验收；不可发布，不可声称已兼容 Revit 2021–2027。**

进入正式 GO 至少需要：

- 对目标年份真实编译成功；
- Revit 2024 加载正常并完成 28 入口烟雾测试；
- 六项专项验收通过；
- 所有模型写入可撤销；
- 重复参数清理不存在值丢失、静默覆盖、错误删除或无法回滚。

## 7. 最终静态结果

- 文本文件：251
- C# 文件：144
- XAML 文件：40
- 窗口/视图根节点：35
- 主题：4
- Ribbon 命令：28
- UI/XAML/XML/资源/事件/UTF-8：错误 0，警告 0
- 重复项目参数清理链路：通过
- Python 工具语法：通过
- 构建准备审计：61 项检查，61 项通过，0 项错误，0 项警告

