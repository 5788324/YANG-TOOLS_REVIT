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
