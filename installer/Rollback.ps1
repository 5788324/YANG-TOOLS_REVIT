[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('2021','2022','2023','2024','2025','2026','2027')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
if (Get-Process Revit -ErrorAction SilentlyContinue) {
    throw 'Revit 正在运行。关闭全部 Revit 窗口后再恢复上一版。'
}
$root = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$Version"
$target = Join-Path $root 'YangTools'
$backup = Join-Path $root 'YangTools.backup'
$manifest = Join-Path $root 'YangTools.Revit.addin'
$manifestBackup = Join-Path $root 'YangTools.Revit.addin.backup'
$tmp = Join-Path $root 'YangTools.rollback_current'
$tmpManifest = Join-Path $root 'YangTools.Revit.addin.rollback_current'
foreach ($path in @($target, $backup, $manifest, $manifestBackup)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "缺少当前版本或上一版备份：$path" }
}
foreach ($path in @($tmp, $tmpManifest)) {
    if (Test-Path -LiteralPath $path) { throw "检测到上次未完成的回退临时文件：$path。请先检查，避免覆盖。" }
}

# 同目录依次交换，不删除任一版本。执行完毕后 .backup 保存刚替换掉的版本。
Move-Item -LiteralPath $target -Destination $tmp
Move-Item -LiteralPath $backup -Destination $target
Move-Item -LiteralPath $tmp -Destination $backup
Move-Item -LiteralPath $manifest -Destination $tmpManifest
Move-Item -LiteralPath $manifestBackup -Destination $manifest
Move-Item -LiteralPath $tmpManifest -Destination $manifestBackup
Write-Host "Revit $Version 已恢复上一版；被替换的版本仍在 YangTools.backup 中。" -ForegroundColor Green
