# 仅检查正在运行的目标 Revit 年份；不能识别进程版本时拒绝改动插件。
function Assert-TargetRevitClosed {
    param([Parameter(Mandatory=$true)][string[]]$Versions)

    $processes = @(Get-CimInstance -ClassName Win32_Process -Filter "Name='Revit.exe'" -ErrorAction Stop)
    foreach ($process in $processes) {
        $path = [string]$process.ExecutablePath
        $match = [regex]::Match($path, '(?i)[\\/]Revit (20\d{2})[\\/]Revit\.exe$')
        $runningVersion = if ($match.Success) {
            $match.Groups[1].Value
        } elseif ($path -and (Test-Path -LiteralPath $path)) {
            $versionInfo = (Get-Item -LiteralPath $path).VersionInfo
            if ($versionInfo.ProductMajorPart -ge 21 -and $versionInfo.ProductMajorPart -lt 40) {
                [string](2000 + $versionInfo.ProductMajorPart)
            }
        }
        if (-not $runningVersion) {
            throw "无法识别 Revit 进程 PID $($process.ProcessId) 的年份或可执行文件路径，已停止安装操作。"
        }
        if ($Versions -contains $runningVersion) {
            throw "目标 Revit $runningVersion 正在运行（PID $($process.ProcessId)）。请关闭该年份的 Revit 后重试；其他年份无需关闭。"
        }
    }
}


function Assert-NoPendingYangToolsRollback {
    param([Parameter(Mandatory=$true)][string]$VersionRoot)

    foreach ($name in @('YangTools.rollback_progress', 'YangTools.rollback_current', 'YangTools.Revit.addin.rollback_current')) {
        if (Test-Path -LiteralPath (Join-Path $VersionRoot $name)) {
            throw "检测到未完成的 YangTools 回退，请先对该年份重新执行「恢复上一版」。目录：$VersionRoot"
        }
    }
}
