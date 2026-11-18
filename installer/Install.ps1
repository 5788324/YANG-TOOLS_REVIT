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
    # 只清理当前未完成的 staging；旧安装目录/manifest 在新 payload 校验之前不得删除。
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction Stop }
    $hadTarget = Test-Path -LiteralPath $target
    $hadManifest = Test-Path -LiteralPath $manifest
    $movedTarget = $false
    $movedManifest = $false
    if (($hadTarget -and -not $hadManifest) -or (-not $hadTarget -and $hadManifest)) {
        throw "Revit $version 插件文件和 manifest 状态不一致，已停止安装；请检查现有文件。"
    }
    if (-not $hadTarget -and ((Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $manifestBackup))) {
        throw "Revit $version 尚有上一版备份但当前插件目录不存在，请先检查或恢复备份。"
    }

    try {
        Copy-Item -LiteralPath $sourcePlugin -Destination $staging -Recurse -Force
        if (-not (Test-Path -LiteralPath (Join-Path $staging 'YangTools.Revit.dll'))) {
            throw '复制到临时目录后未找到 YangTools.Revit.dll。'
        }
        if ($hadTarget) {
            # 保留最新的上一版；只有新 payload 已备好才替换旧备份。
            if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction Stop }
            if (Test-Path -LiteralPath $manifestBackup) { Remove-Item -LiteralPath $manifestBackup -Force -ErrorAction Stop }
            Move-Item -LiteralPath $target -Destination $backup
            $movedTarget = $true
            Move-Item -LiteralPath $manifest -Destination $manifestBackup
            $movedManifest = $true
        }
        Move-Item -LiteralPath $staging -Destination $target
        Copy-Item -LiteralPath $sourceManifest -Destination $manifest -Force
        # 安装成功后保留 YangTools.backup 和 manifest.backup，以便一键回退。
    }
    catch {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        if ($movedTarget) {
            Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
            Move-Item -LiteralPath $backup -Destination $target
        }
        elseif (-not $hadTarget) {
            Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        }
        if ($movedManifest) {
            Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
            Move-Item -LiteralPath $manifestBackup -Destination $manifest
        }
        elseif (-not $hadManifest) {
            Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}

try {
    Stop-IfRevitRunning
    if (-not $Versions -or $Versions.Count -eq 0) {
        throw '请明确指定 Revit 年份，例如 -Versions 2025；或使用中文图形安装器选择年份。不会自动安装全部版本。'
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
