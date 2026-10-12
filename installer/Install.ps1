[CmdletBinding()]
param(
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string[]]$Versions
)

$ErrorActionPreference = 'Stop'
$SupportedVersions = @('2021','2022','2023','2024','2025','2026','2027')
$PayloadRoot = Join-Path $PSScriptRoot 'payload'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ArtifactsPayloadRoot = Join-Path $ProjectRoot 'artifacts\payload'
$BuildOutputRoot = Join-Path $ProjectRoot 'src\YangTools.Revit\bin\x64'
$ProjectManifest = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.addin'
$AddinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'

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


function Get-VersionSource([string]$version) {
    $candidates = @(
        [pscustomobject]@{ PluginRoot = Join-Path $BuildOutputRoot $version; Manifest = $ProjectManifest; Label = '本地编译输出' },
        [pscustomobject]@{ PluginRoot = Join-Path (Join-Path $PayloadRoot $version) 'YangTools'; Manifest = Join-Path (Join-Path $PayloadRoot $version) 'YangTools.Revit.addin'; Label = '正式安装包' },
        [pscustomobject]@{ PluginRoot = Join-Path (Join-Path $ArtifactsPayloadRoot $version) 'YangTools'; Manifest = Join-Path (Join-Path $ArtifactsPayloadRoot $version) 'YangTools.Revit.addin'; Label = '本地打包产物' }
    )
    foreach ($candidate in $candidates) {
        $dll = Join-Path $candidate.PluginRoot 'YangTools.Revit.dll'
        $sharedStyles = Join-Path $candidate.PluginRoot 'UI\SharedStyles.xaml'
        $lightTheme = Join-Path $candidate.PluginRoot 'UI\Themes\LightTheme.xaml'
        $darkTheme = Join-Path $candidate.PluginRoot 'UI\Themes\DarkTheme.xaml'
        $blueTheme = Join-Path $candidate.PluginRoot 'UI\Themes\ProfBlueTheme.xaml'
        $appleTheme = Join-Path $candidate.PluginRoot 'UI\Themes\AppleTheme.xaml'
        $requiredFiles = @($dll, $candidate.Manifest, $sharedStyles, $lightTheme, $darkTheme, $blueTheme, $appleTheme)
        if (@($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -eq 0) {
            $candidate | Add-Member -NotePropertyName Dll -NotePropertyValue $dll -Force
            return $candidate
        }
    }
    return $null
}

function Stop-IfRevitRunning {
    if (Get-Process Revit -ErrorAction SilentlyContinue) {
        throw 'Revit 正在运行。请先关闭全部 Revit 窗口，再重新安装。'
    }
}

function Get-DetectedVersions {
    $detected = @()
    foreach ($version in $SupportedVersions) {
        $program = Join-Path $env:ProgramFiles "Autodesk\Revit $version"
        $addin = Join-Path $AddinsRoot $version
        if ((Test-Path -LiteralPath $program) -or (Test-Path -LiteralPath $addin)) {
            $detected += $version
        }
    }
    return $detected
}

function Install-Version([string]$version) {
    $source = Get-VersionSource $version
    if ($null -eq $source) {
        throw "没有找到 Revit $version 的独立编译文件。"
    }
    $sourcePlugin = $source.PluginRoot
    $sourceDll = $source.Dll
    $sourceManifest = $source.Manifest

    $versionRoot = Join-Path $AddinsRoot $version
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
        Copy-Item -LiteralPath $sourcePlugin -Destination $staging -Recurse -Force
        if (-not (Test-Path -LiteralPath (Join-Path $staging 'YangTools.Revit.dll'))) {
            throw '复制到临时目录后未找到 YangTools.Revit.dll。'
        }

        if (Test-Path -LiteralPath $target) {
            Move-Item -LiteralPath $target -Destination $backup
        }
        if (Test-Path -LiteralPath $manifest) {
            Move-Item -LiteralPath $manifest -Destination $manifestBackup
        }
        Move-Item -LiteralPath $staging -Destination $target
        Copy-Item -LiteralPath $sourceManifest -Destination $manifest -Force
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $manifestBackup -Force -ErrorAction SilentlyContinue
        Write-Host "[完成] Revit $version" -ForegroundColor Green
    }
    catch {
        # 无论旧版本是否存在，都先清理本次可能留下的半成品，再恢复备份。
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $backup) {
            Move-Item -LiteralPath $backup -Destination $target -Force
        }
        if (Test-Path -LiteralPath $manifestBackup) {
            Move-Item -LiteralPath $manifestBackup -Destination $manifest -Force
        }
        throw
    }
}

try {
    Stop-IfRevitRunning
    $removedAssistantData = @(Remove-LegacyAssistantData)
    if ($removedAssistantData.Count -gt 0) {
        Write-Host "已清理旧 AI/摸鱼助手数据：$($removedAssistantData.Count) 项。" -ForegroundColor DarkCyan
    }
    if (-not $Versions -or $Versions.Count -eq 0) {
        $Versions = @(Get-DetectedVersions | Where-Object { $null -ne (Get-VersionSource $_) })
        if ($Versions.Count -eq 0) {
            $Versions = @($SupportedVersions | Where-Object { $null -ne (Get-VersionSource $_) })
            Write-Host '未检测到 Revit 安装目录，将安装当前可用的全部独立编译版本。' -ForegroundColor Yellow
        }
    }

    if ($Versions.Count -eq 0) { throw '没有找到任何可安装的独立编译版本。' }
    $available = @($Versions | Where-Object { $null -ne (Get-VersionSource $_) })
    $skipped = @($Versions | Where-Object { $null -eq (Get-VersionSource $_) })
    foreach ($version in $skipped) {
        Write-Host "[跳过] Revit $version：没有该年份独立编译文件。" -ForegroundColor Yellow
    }
    if ($available.Count -eq 0) { throw '所选版本均没有独立编译文件。' }
    Write-Host "将安装：Revit $($available -join '、Revit ')"
    foreach ($version in $available) { Install-Version $version }
    Write-Host 'YangTools 安装完成，请启动 Revit。' -ForegroundColor Cyan
}
catch {
    Write-Host "安装失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
