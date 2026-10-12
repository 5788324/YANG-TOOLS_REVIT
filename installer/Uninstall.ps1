[CmdletBinding()]
param(
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string[]]$Versions = @('2021','2022','2023','2024','2025','2026','2027')
)

$ErrorActionPreference = 'Stop'
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


try {
    if (Get-Process Revit -ErrorAction SilentlyContinue) {
        throw 'Revit 正在运行。请先关闭全部 Revit 窗口，再重新卸载。'
    }

    $removedAssistantData = @(Remove-LegacyAssistantData)
    if ($removedAssistantData.Count -gt 0) {
        Write-Host "[完成] 已清理旧 AI/摸鱼助手历史、设置和 API Key：$($removedAssistantData.Count) 项。" -ForegroundColor Green
    }

    $removed = 0
    foreach ($version in $Versions) {
        $removedForVersion = $false
        $versionRoot = Join-Path $AddinsRoot $version
        foreach ($folderName in @('YangTools', 'YangTools.installing', 'YangTools.backup')) {
            $folder = Join-Path $versionRoot $folderName
            if (Test-Path -LiteralPath $folder) {
                Remove-Item -LiteralPath $folder -Recurse -Force
                $removed++
                $removedForVersion = $true
            }
        }
        $manifest = Join-Path $versionRoot 'YangTools.Revit.addin'
        $manifestBackup = Join-Path $versionRoot 'YangTools.Revit.addin.backup'
        foreach ($manifestFile in @($manifest, $manifestBackup)) {
            if (Test-Path -LiteralPath $manifestFile) {
                Remove-Item -LiteralPath $manifestFile -Force
                $removed++
                $removedForVersion = $true
            }
        }
        if ($removedForVersion) { Write-Host "[完成] Revit $version" -ForegroundColor Green }
    }

    if ($removed -eq 0) {
        Write-Host '没有检测到 YangTools，无需卸载。' -ForegroundColor Yellow
    } else {
        Write-Host 'YangTools 卸载完成；旧 AI/摸鱼助手数据已清理，其他仍在使用的个人设置保留。' -ForegroundColor Cyan
    }
}
catch {
    Write-Host "卸载失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
