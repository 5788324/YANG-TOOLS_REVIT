[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Assert-TargetRevitClosed.ps1')
Assert-TargetRevitClosed -Versions @($Version)
$root = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$Version"
$target = Join-Path $root 'YangTools'
$backup = Join-Path $root 'YangTools.backup'
$manifest = Join-Path $root 'YangTools.Revit.addin'
$manifestBackup = Join-Path $root 'YangTools.Revit.addin.backup'
$tmp = Join-Path $root 'YangTools.rollback_current'
$tmpManifest = Join-Path $root 'YangTools.Revit.addin.rollback_current'
$progress = Join-Path $root 'YangTools.rollback_progress'
$completed = $progress + '.complete'
if (Test-Path -LiteralPath $completed) {
    Remove-Item -LiteralPath $completed -Recurse -Force
    Write-Host "Revit $Version 的回退已完成。" -ForegroundColor Green
    return
}
if (-not (Test-Path -LiteralPath $progress)) {
    foreach ($path in @($tmp, $tmpManifest)) {
        if (Test-Path -LiteralPath $path) {
            throw "发现旧版回退留下的临时文件：$path。请保留现有目录和 .addin 备份，核对当前版及上一版后再恢复。"
        }
    }
    foreach ($path in @($target, $backup, $manifest, $manifestBackup)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "缺少当前版本或上一版备份：$path" }
    }
    New-Item -ItemType Directory -Path $progress | Out-Null
}

$steps = @(
    @{ From = $target; To = $tmp },
    @{ From = $backup; To = $target },
    @{ From = $tmp; To = $backup },
    @{ From = $manifest; To = $tmpManifest },
    @{ From = $manifestBackup; To = $manifest },
    @{ From = $tmpManifest; To = $manifestBackup }
)
for ($index = 0; $index -lt $steps.Count; $index++) {
    $done = Join-Path $progress "$index.done"
    if (Test-Path -LiteralPath $done) { continue }

    $step = $steps[$index]
    $sourceExists = Test-Path -LiteralPath $step.From
    $destinationExists = Test-Path -LiteralPath $step.To
    if ($sourceExists -eq $destinationExists) {
        throw "回退第 $($index + 1) 步的文件状态无法确认。请保留当前版本、备份和回退记录：$progress"
    }
    if ($sourceExists) {
        Move-Item -LiteralPath $step.From -Destination $step.To
    }
    New-Item -ItemType File -Path $done | Out-Null
}
# 完成标记需与交换记录一起消失，避免删除中途丢掉已完成的步骤。
Move-Item -LiteralPath $progress -Destination $completed
Remove-Item -LiteralPath $completed -Recurse -Force
Write-Host "Revit $Version 已恢复上一版；被替换的版本仍在 YangTools.backup 中。" -ForegroundColor Green
