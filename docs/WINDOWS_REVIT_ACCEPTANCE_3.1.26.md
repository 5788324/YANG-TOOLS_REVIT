# YangTools 3.1.26 Windows / Revit 验收单

> 执行者只负责 Windows、编译、部署和 Revit GUI 验收，不修改架构、不新增功能。发现问题时记录完整异常和 Journal，不做零碎猜测式补丁。

## 1. 源码与静态检查

在全新空目录解压源码，依次运行：

```powershell
.\核验源码.cmd
.\静态检查面板与编码.cmd
```

预期：两个检查均通过；静态报告显示 39 XAML、34 XAML roots、27 Ribbon commands、errors=0、warnings=0。

## 2. 编译

至少先执行：

```powershell
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2024 -p:Platform=x64 -p:DeployAfterBuild=false
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2025 -p:Platform=x64 -p:DeployAfterBuild=false
```

全版本安装包需要：

```powershell
.\tools\Build-All.ps1
.\tools\Package-Installer.ps1 -SkipBuild
```

必须记录每个年份 error/warning 数和 DLL 路径。不得复制其他年份 DLL。

## 3. 图形安装器

1. 解压 `artifacts\YangTools-Installer-3.1.26.zip`。
2. 双击 `YangTools安装器.cmd`。
3. 确认窗口显示 Revit 2021–2027 状态。
4. 选择 Revit 2024，执行“安装 / 更新”。
5. 核对：
   - `%APPDATA%\Autodesk\Revit\Addins\2024\YangTools.Revit.addin`
   - `%APPDATA%\Autodesk\Revit\Addins\2024\YangTools\YangTools.Revit.dll`
6. Revit 运行时再次点击安装，必须明确拒绝。

## 4. Revit 2024 面板烟雾测试

打开一个可修改的普通测试项目。每个窗口只检查：能打开、中文无乱码、主题资源正常、关闭不崩溃。

### 直接打开/先显示配置窗口的 24 个入口

- 对齐到线
- 对齐文本
- 批处理与云链接
- 布尔几何
- 中文检查
- 沿线布柱
- AI 助手
- 等距分布
- 实体生成(Loft)
- 基于面转换
- 族实例管理
- 族文档管理
- 线性布置
- 文本合并
- 临时插件箱
- 从 CAD 粘贴
- 项目资产管理器
- 文件信息
- 系统设置
- 图纸管理
- 实体提取
- 文本修改
- 覆盖清理
- 可见性拷贝

### 需要前置选择后才显示窗口

- 标注替换：先选择至少一个标注。
- 标高修改：先选择至少一个支持标高/偏移的图元。

### 没有 WPF 面板

- 剖面(By Line)：只验证拾取一根直线后能进入命令流程；取消应返回 Cancelled，不应报错。

## 5. 重点回归

- 系统设置切换四套主题，已打开窗口的 DynamicResource 应同步变化。
- AI 助手首次点击才创建；再次点击隐藏，再次点击显示；Revit 启动时不得自动展开。
- 族文档管理、族实例管理、项目资产管理器、图纸管理重复点击时只激活已有窗口，不创建第二实例。
- 打开本地项目后再打开 ACC 云模型，确认没有 `PresentationFramework.dll` / `XamlParseException` 崩溃。
- 工作/会话两个 AI 面板均能显示；会话模式不得读取 Revit 上下文或执行 Python。
- 系统设置中的 MCP 与执行服务页面可打开；默认网关关闭。

## 6. 卸载

1. 关闭 Revit。
2. 图形安装器选择 2024，点击“卸载”。
3. 确认 addin manifest 和 YangTools 插件目录删除。
4. 确认 `%APPDATA%\YangTools` 仍保留。

## 7. 返回格式

```text
环境：Windows / Revit / .NET SDK 版本
源码核验：通过/失败
静态面板检查：通过/失败
2024 编译：error / warning / DLL
2025 编译：error / warning / DLL
2021-2027 全编译：逐年结果
安装器：安装 / 更新 / Revit运行阻止 / 卸载
24 个直接窗口：逐项 PASS/FAIL
2 个前置选择窗口：逐项 PASS/FAIL
剖面命令：PASS/FAIL
主题切换：PASS/FAIL
AI 窗口：PASS/FAIL
ACC 云模型：PASS/FAIL
MCP 状态页：PASS/FAIL
Journal/异常：完整路径与原文
最终结论：GO / NO-GO
```
