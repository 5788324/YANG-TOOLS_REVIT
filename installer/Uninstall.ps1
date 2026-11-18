[CmdletBinding()]
param(
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string[]]$Versions
)

$ErrorActionPreference = 'Stop'
$AddinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'



try {
    if (-not $Versions -or $Versions.Count -eq 0) { throw '请明确指定 Revit 年份，例如 -Versions 2025；或使用中文图形安装器。' }
    if (Get-Process Revit -ErrorAction SilentlyContinue) {
        throw 'Revit 正在运行。请先关闭全部 Revit 窗口，再重新卸载。'
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
        Write-Host 'YangTools 卸载完成；个人设置与历史数据均已保留。' -ForegroundColor Cyan
    }
}
catch {
    Write-Host "卸载失败：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
