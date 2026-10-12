[CmdletBinding()]
param(
    [string[]]$Versions = @('2021','2022','2023','2024','2025','2026','2027'),
    [switch]$NoRestore,
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'
$Artifacts = Join-Path $ProjectRoot 'artifacts\payload'
$Manifest = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.addin'

$YangToolsUserDataRoot = Join-Path $env:APPDATA 'YangTools'
$LegacyAssistantDirectory = Join-Path $YangToolsUserDataRoot 'Assistant'
$LegacyAssistantFiles = @(
    (Join-Path $YangToolsUserDataRoot 'copilot_settings.json'),
    (Join-Path $YangToolsUserDataRoot 'copilot_history.json'),
    (Join-Path $YangToolsUserDataRoot 'moyu_settings.json'),
    (Join-Path $YangToolsUserDataRoot 'moyu_history.json')
)

function Remove-LegacyAssistantData {
    $removed = @()
    if (Test-Path -LiteralPath $LegacyAssistantDirectory) {
        Remove-Item -LiteralPath $LegacyAssistantDirectory -Recurse -Force -ErrorAction Stop
        $removed += $LegacyAssistantDirectory
    }
    foreach ($path in $LegacyAssistantFiles) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force -ErrorAction Stop
            $removed += $path
        }
    }
    return @($removed)
}



if ($Deploy -and (Get-Process Revit -ErrorAction SilentlyContinue)) {
    throw 'Revit 正在运行。请先关闭全部 Revit 窗口，再执行部署。'
}

if ($Deploy) {
    $removedAssistantData = @(Remove-LegacyAssistantData)
    if ($removedAssistantData.Count -gt 0) {
        Write-Host "已清理旧 AI/摸鱼助手历史、设置和 API Key：$($removedAssistantData.Count) 项。" -ForegroundColor DarkCyan
    }
}

function Deploy-Version([string]$Version, [string]$SourcePlugin, [string]$SourceManifest) {
    $versionRoot = Join-Path (Join-Path $env:APPDATA 'Autodesk\Revit\Addins') $Version
    $target = Join-Path $versionRoot 'YangTools'
    $manifest = Join-Path $versionRoot 'YangTools.Revit.addin'
    $staging = Join-Path $versionRoot 'YangTools.installing'
    $backup = Join-Path $versionRoot 'YangTools.backup'
    $manifestBackup = Join-Path $versionRoot 'YangTools.Revit.addin.backup'

    New-Item -ItemType Directory -Force -Path $versionRoot | Out-Null
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $manifestBackup -Force -ErrorAction SilentlyContinue

    try {
        Copy-Item -LiteralPath $SourcePlugin -Destination $staging -Recurse -Force
        if (-not (Test-Path -LiteralPath (Join-Path $staging 'YangTools.Revit.dll'))) {
            throw "Revit $Version 部署临时目录缺少 YangTools.Revit.dll。"
        }

        if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $backup }
        if (Test-Path -LiteralPath $manifest) { Move-Item -LiteralPath $manifest -Destination $manifestBackup }
        Move-Item -LiteralPath $staging -Destination $target
        Copy-Item -LiteralPath $SourceManifest -Destination $manifest -Force

        # 3.1.30 已将重复参数清理并入 YangTools，移除本项目历史独立插件入口，避免功能区出现两套按钮。
        Remove-Item -LiteralPath (Join-Path $versionRoot 'RevitParameterDeduplicator.addin') -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $versionRoot 'RevitParameterDeduplicator') -Recurse -Force -ErrorAction SilentlyContinue

        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $manifestBackup -Force -ErrorAction SilentlyContinue
        Write-Host "已部署 Revit $Version：$versionRoot" -ForegroundColor Green
    }
    catch {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $backup) { Move-Item -LiteralPath $backup -Destination $target -Force }
        if (Test-Path -LiteralPath $manifestBackup) { Move-Item -LiteralPath $manifestBackup -Destination $manifest -Force }
        throw
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '找不到 dotnet。请安装 .NET 8 SDK、.NET 10 SDK 和 .NET Framework 4.8 Developer Pack。'
}

Remove-Item $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
foreach ($version in $Versions) {
    Write-Host "=== 构建 Revit $version ===" -ForegroundColor Cyan
    $arguments = @('build', $Project, '-c', $version, '-p:Platform=x64', '-p:DeployAfterBuild=false')
    if ($NoRestore) { $arguments += '--no-restore' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Revit $version 构建失败。" }

    $output = Join-Path $ProjectRoot "src\YangTools.Revit\bin\x64\$version"
    $dll = Join-Path $output 'YangTools.Revit.dll'
    if (-not (Test-Path $dll)) { throw "Revit $version 输出中缺少 YangTools.Revit.dll。" }

    $targetRoot = Join-Path $Artifacts $version
    $targetPlugin = Join-Path $targetRoot 'YangTools'
    New-Item -ItemType Directory -Force -Path $targetPlugin | Out-Null
    Get-ChildItem $output -File -Recurse | Where-Object {
        $_.Extension -ne '.pdb' -and
        $_.Name -notin @('RevitAPI.dll', 'RevitAPIUI.dll')
    } | ForEach-Object {
        $relative = $_.FullName.Substring($output.Length).TrimStart('\')
        $destination = Join-Path $targetPlugin $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item $_.FullName $destination -Force
    }
    $artifactManifest = Join-Path $targetRoot 'YangTools.Revit.addin'
    Copy-Item $Manifest $artifactManifest -Force

    if ($Deploy) {
        Deploy-Version -Version $version -SourcePlugin $targetPlugin -SourceManifest $artifactManifest
    }
}

Write-Host "全部构建完成：$Artifacts" -ForegroundColor Green
if ($Deploy) { Write-Host '所选年份已完成当前用户级部署。' -ForegroundColor Green }
