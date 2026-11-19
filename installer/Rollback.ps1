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
$checkpoint = Join-Path $root 'YangTools.rollback-step'
$paths = @($target, $backup, $tmp, $manifest, $manifestBackup, $tmpManifest)
$moves = @(@(0, 2), @(1, 0), @(2, 1), @(3, 5), @(4, 3), @(5, 4))
# 当前、备份、临时各三位，分别对应 DLL 目录和 .addin。
$layouts = @('110110', '011110', '101110', '110110', '110011', '110101', '110110')

function Get-RollbackLayout {
    $bits = for ($i = 0; $i -lt $paths.Count; $i++) {
        if (Test-Path -LiteralPath $paths[$i]) {
            $type = if ($i -lt 3) { 'Container' } else { 'Leaf' }
            if (-not (Test-Path -LiteralPath $paths[$i] -PathType $type)) {
                throw "回退路径类型不正确：$($paths[$i])"
            }
            '1'
        } else { '0' }
    }
    return ($bits -join '')
}

function Save-RollbackStep([int]$Step) {
    $staging = $checkpoint + '.next'
    [IO.File]::WriteAllText($staging, [string]$Step)
    if ([IO.File]::Exists($checkpoint)) {
        [IO.File]::Replace($staging, $checkpoint, [NullString]::Value)
    } else {
        [IO.File]::Move($staging, $checkpoint)
    }
}

if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "缺少 Revit $Version 的插件目录：$root"
}
$lock = Open-YangToolsAddinsOperation -VersionRoot $root
try {
    $step = 0
    $layout = Get-RollbackLayout
    if (Test-Path -LiteralPath $checkpoint) {
        if (-not [int]::TryParse([IO.File]::ReadAllText($checkpoint), [ref]$step) -or
            $step -lt 0 -or $step -gt $moves.Count) {
            throw "回退进度文件无效：$checkpoint"
        }
    } else {
        # 兼容旧脚本留下了临时目录或清单的可识别中断状态。
        $legacySteps = @{ '110110' = 0; '011110' = 1; '101110' = 2; '110011' = 4; '110101' = 5 }
        if (-not $legacySteps.ContainsKey($layout)) {
            throw "当前版本、备份和临时文件的布局无法识别。请保留文件并检查：$root"
        }
        $step = $legacySteps[$layout]
        Save-RollbackStep $step
    }

    for (; $step -lt $moves.Count; $step++) {
        $layout = Get-RollbackLayout
        if ($layout -eq $layouts[$step]) {
            $from = $paths[$moves[$step][0]]
            $to = $paths[$moves[$step][1]]
            if ($step -lt 3) { [IO.Directory]::Move($from, $to) }
            else { [IO.File]::Move($from, $to) }
        } elseif ($layout -ne $layouts[$step + 1]) {
            throw "回退进度与磁盘状态不一致。请保留文件并检查：$root"
        }
        Save-RollbackStep ($step + 1)
    }

    if ((Get-RollbackLayout) -ne $layouts[-1]) {
        throw "回退后的目录和清单不完整。请保留文件并检查：$root"
    }
    [IO.File]::Delete($checkpoint + '.next')
    Remove-Item -LiteralPath $checkpoint
}
finally { $lock.Dispose() }
Write-Host "Revit $Version 已恢复上一版；被替换的版本仍在 YangTools.backup 中。" -ForegroundColor Green
