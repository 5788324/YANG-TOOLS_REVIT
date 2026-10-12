# YangTools 3.1.27 Windows / Revit 验收单

## A. 全新目录

- 删除旧源码解压目录，不覆盖解压。
- 解压 `YangTools_Project_3.1.27_Source_CLEAN.zip`。
- 运行 `核验源码.cmd`。
- 运行 `静态检查面板与编码.cmd`。

预期：两项均通过；静态报告中 `errors=0`，`external-static-resource` 为 0。

## B. Revit 2024 编译部署

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-All.ps1 -Versions 2024
```

或使用现有 DeepSeek 编译部署流程，但必须确认输出目录包含：

```text
src\YangTools.Revit\bin\x64\2024\YangTools.Revit.dll
src\YangTools.Revit\bin\x64\2024\UI\SharedStyles.xaml
src\YangTools.Revit\bin\x64\2024\UI\Themes\*.xaml
```

## C. 源码模式安装器

- 双击 `installer\YangTools安装器.cmd`。
- Revit 2024 应显示“可安装：本地编译输出”或“可安装：正式安装包”。
- 即使 Revit 2021 显示“缺少该年份编译文件”，选择 2024 后仍应安装成功。
- 若误选 2021 与 2024，应跳过 2021、继续安装 2024，而不是整体失败。

## D. 高风险面板烟雾测试

依次打开并关闭：

1. AI 助手（工作/会话两个子面板均显示）
2. 批处理与云链接
3. 项目中文检查
4. 族文档管理
5. 族实例管理
6. 图纸与图框管理

预期：不出现 `StaticResourceExtension`、`ResourceDictionary.Source`、`Style` 转 `Brush` 或黑屏崩溃。

## E. 其余入口

逐个点击 27 个 Ribbon 入口。

- “标注替换”“标高修改”先准备对应选择。
- “剖面(By Line)”没有 WPF 面板，只验证命令进入正常选择流程。
- 任何失败请提供 `%LOCALAPPDATA%\YangTools\Logs\YangTools-YYYY-MM-DD.log`。3.1.27 日志/错误框应显示具体面板名和最深层资源错误。

## F. 正式部分年份安装包

仅打包 2024：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Package-Installer.ps1 -SkipBuild -Versions 2024
```

预期生成：

```text
artifacts\YangTools-Installer-3.1.27.zip
```

七年份正式发布才使用：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Package-Installer.ps1 -RequireAll
```
