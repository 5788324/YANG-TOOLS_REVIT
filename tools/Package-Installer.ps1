[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string[]]$Versions,
    [switch]$RequireAll,
    [switch]$AllowPartial
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ProjectFile = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'
$Artifacts = Join-Path $ProjectRoot 'artifacts'
$Payload = Join-Path $Artifacts 'payload'
$SupportedVersions = @('2021','2022','2023','2024','2025','2026','2027')

[xml]$ProjectXml = Get-Content -LiteralPath $ProjectFile -Raw -Encoding UTF8
$VersionNodes = @($ProjectXml.SelectNodes('/Project/PropertyGroup/Version'))
if ($VersionNodes.Count -ne 1) { throw "无法确定项目版本，Version 节点数量：$($VersionNodes.Count)。" }
$Version = [string]$VersionNodes[0].InnerText
$Package = Join-Path $Artifacts "YangTools-Installer-$Version"
$Zip = "$Package.zip"

if (-not $SkipBuild) {
    if ($Versions -and $Versions.Count -gt 0) {
        & (Join-Path $PSScriptRoot 'Build-All.ps1') -Versions $Versions
    }
    else {
        & (Join-Path $PSScriptRoot 'Build-All.ps1')
    }
}

# 已经执行 dotnet build/deploy 时，从 bin\x64\年份收集真实构建产物。
$Manifest = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.addin'
$TargetVersions = if ($Versions -and $Versions.Count -gt 0) { @($Versions) } else { @($SupportedVersions) }
foreach ($RevitVersion in $TargetVersions) {
    $VersionRoot = Join-Path $Payload $RevitVersion
    $PayloadDll = Join-Path $VersionRoot 'YangTools\YangTools.Revit.dll'
    $Output = Join-Path $ProjectRoot "src\YangTools.Revit\bin\x64\$RevitVersion"
    $OutputDll = Join-Path $Output 'YangTools.Revit.dll'

    if (-not (Test-Path -LiteralPath $OutputDll)) {
        if (Test-Path -LiteralPath $PayloadDll) { continue }
        continue
    }

    # 本地 bin 是 DeepSeek/Codex 最近一次真实编译结果，覆盖可能陈旧的 artifacts payload。
    Remove-Item -LiteralPath $VersionRoot -Recurse -Force -ErrorAction SilentlyContinue
    $TargetPlugin = Join-Path $VersionRoot 'YangTools'
    New-Item -ItemType Directory -Force -Path $TargetPlugin | Out-Null
    Get-ChildItem -LiteralPath $Output -File -Recurse | Where-Object {
        $_.Extension -ne '.pdb' -and $_.Name -notin @('RevitAPI.dll', 'RevitAPIUI.dll')
    } | ForEach-Object {
        $relative = $_.FullName.Substring($Output.Length).TrimStart('\')
        $destination = Join-Path $TargetPlugin $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
    Copy-Item -LiteralPath $Manifest -Destination (Join-Path $VersionRoot 'YangTools.Revit.addin') -Force
    Write-Host "已收集 Revit $RevitVersion 本地编译输出。" -ForegroundColor DarkCyan
}

if (-not (Test-Path -LiteralPath $Payload)) { throw '没有找到任何构建产物。请先编译至少一个 Revit 年份。' }

$MissingVersions = @()
foreach ($RevitVersion in $SupportedVersions) {
    $VersionRoot = Join-Path $Payload $RevitVersion
    if (-not (Test-Path -LiteralPath (Join-Path $VersionRoot 'YangTools\YangTools.Revit.dll')) -or
        -not (Test-Path -LiteralPath (Join-Path $VersionRoot 'YangTools.Revit.addin'))) {
        $MissingVersions += $RevitVersion
    }
}
if ($MissingVersions.Count -gt 0 -and $RequireAll) {
    throw "安装包缺少这些年份的真实构建产物：$($MissingVersions -join '、')。已指定 -RequireAll，因此停止打包。"
}

$AvailableVersions = @($SupportedVersions | Where-Object {
    Test-Path -LiteralPath (Join-Path (Join-Path $Payload $_) 'YangTools\YangTools.Revit.dll')
})
if ($Versions -and $Versions.Count -gt 0) {
    $AvailableVersions = @($AvailableVersions | Where-Object { $_ -in $Versions })
}
if ($AvailableVersions.Count -eq 0) { throw '没有任何可打包的真实构建版本。' }

Remove-Item -LiteralPath $Package -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $Zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Package | Out-Null
Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'installer') -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $Package -Force
}
$PackagePayload = Join-Path $Package 'payload'
New-Item -ItemType Directory -Force -Path $PackagePayload | Out-Null
foreach ($RevitVersion in $AvailableVersions) {
    Copy-Item -LiteralPath (Join-Path $Payload $RevitVersion) -Destination $PackagePayload -Recurse -Force
}

$Readme = @"
YangTools $Version 快速安装包

推荐入口：双击“YangTools安装器.cmd”打开中文图形安装器。
备用入口：安装.cmd / 卸载.cmd。

安装位置：%APPDATA%\Autodesk\Revit\Addins\{年份}
权限要求：仅当前用户，无需管理员权限。
注意：安装、更新或卸载前必须关闭全部 Revit 窗口。
AI/摸鱼助手已永久删除；安装、更新或卸载会清理 `%APPDATA%\YangTools\Assistant` 以及旧 copilot/moyu 设置与历史文件。
不同 Revit 年份必须使用 payload 中各自独立编译的 DLL，禁止跨年份复制。
本安装包包含：Revit $($AvailableVersions -join '、Revit ')。
未包含的年份不会影响已包含年份的安装。
"@
[System.IO.File]::WriteAllText((Join-Path $Package '安装说明.txt'), $Readme, (New-Object System.Text.UTF8Encoding($true)))

$HashLines = Get-ChildItem -LiteralPath $Package -File -Recurse | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($Package.Length).TrimStart('\')
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    "$hash  $relative"
}
[System.IO.File]::WriteAllLines((Join-Path $Package 'SHA256SUMS.txt'), $HashLines, (New-Object System.Text.UTF8Encoding($false)))

Compress-Archive -Path (Join-Path $Package '*') -DestinationPath $Zip -CompressionLevel Optimal
Write-Host "安装包已生成：$Zip" -ForegroundColor Green
Write-Host "包含版本：Revit $($AvailableVersions -join '、Revit ')" -ForegroundColor Cyan
if ($MissingVersions.Count -gt 0) {
    Write-Host "未包含版本：Revit $($MissingVersions -join '、Revit ')（不影响已包含版本）" -ForegroundColor Yellow
}
