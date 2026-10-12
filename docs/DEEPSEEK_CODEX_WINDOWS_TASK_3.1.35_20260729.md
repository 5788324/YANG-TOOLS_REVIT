# DeepSeek / Codex 执行任务：YangTools 3.1.35 Windows 编译、部署与 Revit 验收

## 0. 角色和硬限制

你负责在用户 Windows 电脑上独立完成源码核验、真实编译、部署、Revit GUI 验收、日志收集和最终报告。用户不参与测试，不向用户追问技术步骤。

必须遵守：

1. 输入源码必须解压到全新的空目录，禁止覆盖旧版本。
2. 不新增功能，不大重构，不改变 UI 需求。
3. 只修真实编译错误或本任务实机复现的确定故障。
4. 不推送 Git，不创建 PR。
5. 每个 Revit 年份使用对应 Configuration 产物，禁止跨年份复制 DLL。
6. Revit 写操作必须在测试项目或副本中执行，必须验证一次撤销。
7. 重复参数清理必须保留原参数、复制内部值、禁止删除重建、禁止名称模糊匹配；发现值丢失、未知级联或唯一性失败必须回滚。
8. 非模态窗口访问 Revit API 必须通过 `ExternalEvent`；禁止 `Task.Run` 直接访问 `Document`。
9. 禁止创建第二个 WPF `Application`，禁止向 Revit 全局资源注入 YangTools 主题。
10. 若修复代码，必须同步 README、PROJECT、WORKLOG、功能矩阵、兼容性说明和 HANDOFF，并重新打包完整源码 ZIP。

## 1. 输入和输出

输入目录：

```text
YangTools_Project_3.1.35_Source_VERIFIED_20260729
```

所有结果写入：

```text
artifacts\windows-validation\20260729\
├─ environment\
├─ source-check\
├─ build\2021 ... 2027\
├─ deploy\
├─ revit-2024\ribbon-28\
├─ revit-2024\special\
├─ revit-versions\
├─ logs\
├─ screenshots\
└─ final\
```

不得只在聊天中口头汇报；必须生成可回收文件。

## 2. 环境盘点

在源码根目录打开 PowerShell，执行并保存输出：

```powershell
$Root = (Get-Location).Path
$Out = Join-Path $Root 'artifacts\windows-validation\20260729'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$PSVersionTable | Out-File "$Out\environment\powershell.txt" -Encoding utf8
cmd /c ver | Out-File "$Out\environment\windows.txt" -Encoding utf8
dotnet --info | Out-File "$Out\environment\dotnet-info.txt" -Encoding utf8
dotnet --list-sdks | Out-File "$Out\environment\dotnet-sdks.txt" -Encoding utf8
dotnet --list-runtimes | Out-File "$Out\environment\dotnet-runtimes.txt" -Encoding utf8

2021..2027 | ForEach-Object {
    $exe = "C:\Program Files\Autodesk\Revit $_\Revit.exe"
    [PSCustomObject]@{ Revit = $_; Installed = Test-Path $exe; Path = $exe }
} | Export-Csv "$Out\environment\revit-installed.csv" -NoTypeInformation -Encoding utf8
```

如缺少 .NET Framework 4.8 Developer Pack、.NET 8 SDK 或 .NET 10 SDK，记录为环境阻断，不得伪装成源码错误。

## 3. 源码核验

执行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

& .\tools\Verify-SourceBaseline.ps1 *>&1 |
    Tee-Object "$Out\source-check\Verify-SourceBaseline.log"
if ($LASTEXITCODE -ne 0) { throw '源码基线核验失败。' }

py -3 .\tools\Test-UiPanelsStatic.py --root . *>&1 |
    Tee-Object "$Out\source-check\Test-UiPanelsStatic.log"
if ($LASTEXITCODE -ne 0) { throw 'UI/编码静态检查失败。' }

py -3 .\tools\Test-ParameterCleanupPipeline.py *>&1 |
    Tee-Object "$Out\source-check\Test-ParameterCleanupPipeline.log"
if ($LASTEXITCODE -ne 0) { throw '重复参数清理链路模拟失败。' }

py -3 -m py_compile .\tools\Test-UiPanelsStatic.py `
    .\tools\Test-ParameterCleanupPipeline.py `
    .\tools\Generate-RibbonIcons.py `
    .\McpServer\server.py
```

验收：全部退出码为 0；静态检查必须为 0 errors / 0 warnings。

## 4. 七配置真实编译

先清理旧输出：

```powershell
Remove-Item .\src\YangTools.Revit\bin -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\src\YangTools.Revit\obj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\payload -Recurse -Force -ErrorAction SilentlyContinue
```

逐年份执行，不要用一个总日志覆盖所有结果：

```powershell
$Project = '.\src\YangTools.Revit\YangTools.Revit.csproj'
$Versions = '2021','2022','2023','2024','2025','2026','2027'
$BuildRows = @()

foreach ($v in $Versions) {
    $dir = "$Out\build\$v"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    dotnet restore $Project -p:Configuration=$v -p:Platform=x64 `
        *>&1 | Tee-Object "$dir\restore.log"
    $restoreExit = $LASTEXITCODE

    if ($restoreExit -eq 0) {
        dotnet build $Project -c $v -p:Platform=x64 -p:DeployAfterBuild=false `
            --no-restore -bl:"$dir\build.binlog" `
            *>&1 | Tee-Object "$dir\build.log"
        $buildExit = $LASTEXITCODE
    }
    else {
        $buildExit = -1
    }

    $dll = ".\src\YangTools.Revit\bin\x64\$v\YangTools.Revit.dll"
    $exists = Test-Path $dll
    $info = if ($exists) { [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $dll)) } else { $null }
    $hash = if ($exists) { (Get-FileHash $dll -Algorithm SHA256).Hash } else { '' }
    $size = if ($exists) { (Get-Item $dll).Length } else { 0 }

    $BuildRows += [PSCustomObject]@{
        Revit = $v
        RestoreExit = $restoreExit
        BuildExit = $buildExit
        DllExists = $exists
        FileVersion = if ($info) { $info.FileVersion } else { '' }
        ProductVersion = if ($info) { $info.ProductVersion } else { '' }
        SizeBytes = $size
        Sha256 = $hash
        Dll = $dll
    }
}

$BuildRows | Export-Csv "$Out\final\BUILD_MATRIX_2021_2027.csv" -NoTypeInformation -Encoding utf8
$BuildRows | Format-Table -AutoSize | Out-File "$Out\final\BUILD_MATRIX_2021_2027.txt" -Encoding utf8
```

每个成功年份必须满足：

- `RestoreExit=0`；
- `BuildExit=0`；
- 存在 `YangTools.Revit.dll`；
- `ProductVersion=3.1.35`；
- 输出目录不含 `RevitAPI.dll`、`RevitAPIUI.dll`；
- 记录 warning 数、SDK、TargetFramework、API 包版本、DLL 大小和 SHA-256。

如果只因本机缺少 SDK、NuGet 或网络失败，标记“环境阻断”；如果 C# 编译错误，标记“源码 NO-GO”，保留完整日志和 binlog。

全部编译后再执行一次正式收集：

```powershell
& .\tools\Build-All.ps1 -Versions 2021,2022,2023,2024,2025,2026,2027 -NoRestore `
    *>&1 | Tee-Object "$Out\build\Build-All.log"
```

## 5. Revit 2024 干净部署

只在 Revit 全部关闭时执行：

```powershell
if (Get-Process Revit -ErrorAction SilentlyContinue) {
    throw 'Revit 仍在运行，禁止部署。'
}

& .\tools\Build-All.ps1 -Versions 2024 -NoRestore -Deploy `
    *>&1 | Tee-Object "$Out\deploy\deploy-2024.log"
```

部署后核对：

```text
%APPDATA%\Autodesk\Revit\Addins\2024\YangTools.Revit.addin
%APPDATA%\Autodesk\Revit\Addins\2024\YangTools\YangTools.Revit.dll
%APPDATA%\Autodesk\Revit\Addins\2024\YangTools\UI\SharedStyles.xaml
%APPDATA%\Autodesk\Revit\Addins\2024\YangTools\UI\Themes\*.xaml
%APPDATA%\Autodesk\Revit\Addins\2024\YangTools\Docs\MicroTool_Developer_Guide V3.md
```

计算已部署 DLL SHA-256，并与构建输出一致。确认历史 `RevitParameterDeduplicator.addin` 和独立目录已移除。

## 6. Revit 2024：28 个 Ribbon 入口烟雾测试

由执行者独立创建测试项目或项目副本，逐个点击 `docs/FEATURE_AUDIT_3.1.35.md` 中的 28 个入口。

每个入口记录：

| 字段 | 要求 |
|---|---|
| 面板/功能 | 中文名称 |
| 启动 | 正常 / 失败 |
| UI | 是否正常显示，是否黑屏、资源异常、窗口被遮挡 |
| 取消 | 取消选择或关闭窗口是否无异常 |
| 最小执行 | 只读结果或最小模型操作 |
| 撤销 | 写模型功能是否一次 Ctrl+Z 恢复 |
| 日志 | Journal、YangTools 日志、截图路径 |
| 结论 | PASS / FAIL / BLOCKED |

重点禁止：

- `StaticResourceExtension` / `XamlParseException`；
- 第二个 WPF Application；
- 主题污染 Revit/ACC；
- 非模态窗口直接在后台线程访问 `Document`；
- 操作失败后留下半完成模型修改。

输出：

```text
$Out\final\RIBBON_28_SMOKE_2024.md
```

## 7. 六项专项验收

### 7.1 重复项目参数清理（P0，先做）

必须使用项目副本。执行顺序：

1. 扫描并保存全部预览/CSV。
2. 使用“测试：最多 10 个来源参数”。
3. 检查 keeper 保留、来源删除数量、绑定、冲突、共享参数和已知引用保护。
4. `08_参数值完整性复核.csv` 必须通过。
5. `09_已选参数唯一性复核.csv` 必须通过。
6. `10_运行诊断.csv` 必须存在且持续记录阶段进度。
7. 抽查 String/Integer/Double/ElementId 内部值；不得只比较显示文本。
8. 未知级联、值丢失、唯一性失败必须使整个事务组回滚。
9. 执行一次 Revit 撤销，确认参数和数值恢复。
10. 小批通过后，再在 RIS_003 同类副本验证约 21891 个值迁移场景。

任何静默值丢失、覆盖冲突值、删除 keeper、删除重建、撤销失败：立即 NO-GO。

### 7.2 临时插件箱

- 无说明 DLL 显示缺少说明，禁止运行和收藏。
- 一键生成说明模板；填写功能说明、使用步骤、适用 Revit 版本和模型影响后可运行。
- 收藏包保留 `microtool.package.json` 说明。
- 打开部署后的 V3.1 开发指南。
- 用带一个私有依赖 DLL 的测试工具验证影子复制和依赖加载。
- 临时 DLL 必须是 Revit 2024 独立编译产物。

### 7.3 文本等距分布

使用至少 4 个宽度不同的 `TextNote`，测试六种模式。记录操作前后 `TextNote.Coord`；第一个排序文本保持原位；一次撤销恢复。

### 7.4 文本基准对齐

测试 X、Y、旋转及组合。源文本不动；锁定/成组目标应被跳过，其他目标仍成功；成功/跳过数准确；一次撤销恢复。

### 7.5 中文检查

准备中文族/类型/字符串参数/共享参数/文字注释/视图/模板/标高/轴网/过滤器/项目信息。验证完整扫描、只读、选择定位和复制 ID；数值显示字符串不产生假阳性；大模型扫描期间窗口已显示并有阶段进度。

### 7.6 系统设置 / 面板设置

- 未保存关闭不落盘。
- 显式保存后重启 Revit 生效。
- 系统设置固定显示。
- 任一面板不能保存为零个主区按钮。
- 全选/当前恢复默认/全部恢复默认可用。
- `ribbon_config.json` 是完整快照，`.tmp/.bak` 与失败恢复有效。
- 全 false 旧坏配置启动时自动恢复至少一个主区入口。

输出：

```text
$Out\final\FEATURE_SPECIAL_ACCEPTANCE_3.1.35.md
```

## 8. 实际安装年份兼容矩阵

只对 `revit-installed.csv` 中 `Installed=True` 的年份部署和启动。未安装年份写“未测试”，不得推断。

每个实际年份至少验证：

1. 对应年份 DLL 独立部署；
2. Revit 启动无加载错误；
3. YangTools 页签和 28 个命令注册；
4. 系统设置、临时插件箱、中文检查、AI DockablePane 可打开；
5. 一个只读功能通过；
6. 一个写模型功能执行并可撤销；
7. 主题、Owner、日志正常。

输出：

```text
$Out\final\REVIT_VERSION_SMOKE_2021_2027.md
```

## 9. 发现问题时的修复规则

1. 先保存完整错误、堆栈、Journal、截图、Revit 年份和复现步骤。
2. 只做最小定点修复，不加功能、不顺手重构。
3. 修改后运行全部源码核验和静态检查。
4. 重新编译受影响年份；至少重新编译并部署 2024。
5. 重新执行失败项及其相邻高风险路径。
6. 更新 README、PROJECT、WORKLOG、`FEATURE_AUDIT_3.1.35.md`、`REVIT_2021_2027_COMPATIBILITY_3.1.35.md`、`HANDOFF_3.1.35.md`。
7. 打包完整源码，不提供零散补丁。

## 10. 最终必须交付

```text
BUILD_MATRIX_2021_2027.md / csv / 所有 build.log / build.binlog
RIBBON_28_SMOKE_2024.md
FEATURE_SPECIAL_ACCEPTANCE_3.1.35.md
REVIT_VERSION_SMOKE_2021_2027.md
ISSUES_AND_FIXES_3.1.35.md
FINAL_VERDICT_3.1.35.md
编译日志、NuGet 还原日志、Revit Journal、YangTools 日志、关键截图
若有修复：最新完整源码 ZIP + SHA-256
所有成功 DLL：文件版本、大小、SHA-256
```

## 11. 判定规则

### GO

- 所需年份真实编译成功；
- Revit 2024 28 入口和六项专项全部通过；
- 无数据完整性、撤销、WPF 宿主或线程问题；
- 实际安装版本的最小兼容矩阵通过。

### 条件 GO

- Revit 2024 和实际使用年份全部通过；
- 未安装年份只能标记未测试；
- 不存在数据安全或宿主稳定性故障；
- 尚未实测年份不得写入已兼容名单。

### NO-GO

任一情况：

- 2024 编译或加载失败；
- 28 入口存在阻断性启动问题；
- 参数清理值丢失、错误覆盖、删除 keeper、无法回滚或撤销失败；
- 第二个 WPF Application、全局资源污染、ACC 崩溃；
- 非模态线程违规访问 Revit API；
- 跨年份复用 DLL；
- 修复后静态检查或编译仍失败。
