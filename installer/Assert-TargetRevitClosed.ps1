# 仅检查正在运行的目标 Revit 年份；不能识别进程版本时拒绝改动插件。
function Assert-TargetRevitClosed {
    param([Parameter(Mandatory=$true)][string[]]$Versions)

    $processes = @(Get-CimInstance -ClassName Win32_Process -Filter "Name='Revit.exe'" -ErrorAction Stop)
    foreach ($process in $processes) {
        $path = [string]$process.ExecutablePath
        $match = [regex]::Match($path, '(?i)[\\/]Revit (20\d{2})[\\/]Revit\.exe$')
        if (-not $match.Success) {
            throw "无法识别 Revit 进程 PID $($process.ProcessId) 的年份或可执行文件路径，已停止安装操作。"
        }
        $runningVersion = $match.Groups[1].Value
        if ($Versions -contains $runningVersion) {
            throw "目标 Revit $runningVersion 正在运行（PID $($process.ProcessId)）。请关闭该年份的 Revit 后重试；其他年份无需关闭。"
        }
    }
}

function Open-YangToolsAddinsOperation {
    param([Parameter(Mandatory=$true)][string]$VersionRoot)

    $path = Join-Path $VersionRoot 'YangTools.operation.lock'
    return [IO.File]::Open($path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
}

function Assert-NoPendingYangToolsRollback {
    param([Parameter(Mandatory=$true)][string]$VersionRoot)

    foreach ($name in @('YangTools.rollback-step', 'YangTools.rollback_current', 'YangTools.Revit.addin.rollback_current')) {
        if (Test-Path -LiteralPath (Join-Path $VersionRoot $name)) {
            throw "检测到未完成的回退。请先用 Rollback.ps1 恢复该年份，再安装或卸载。"
        }
    }
}
