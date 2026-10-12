[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

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
$script:VersionByIndex = @{}

function Test-RevitRunning {
    return $null -ne (Get-Process Revit -ErrorAction SilentlyContinue)
}

function Test-RevitDetected([string]$Version) {
    $programPath = Join-Path $env:ProgramFiles "Autodesk\Revit $Version"
    $userAddinPath = Join-Path $AddinsRoot $Version
    return (Test-Path -LiteralPath $programPath) -or (Test-Path -LiteralPath $userAddinPath)
}

function Get-VersionSource([string]$Version) {
    $candidates = @(
        [pscustomobject]@{
            PluginRoot = Join-Path $BuildOutputRoot $Version
            Manifest = $ProjectManifest
            Label = '本地编译输出'
        },
        [pscustomobject]@{
            PluginRoot = Join-Path (Join-Path $PayloadRoot $Version) 'YangTools'
            Manifest = Join-Path (Join-Path $PayloadRoot $Version) 'YangTools.Revit.addin'
            Label = '正式安装包'
        },
        [pscustomobject]@{
            PluginRoot = Join-Path (Join-Path $ArtifactsPayloadRoot $Version) 'YangTools'
            Manifest = Join-Path (Join-Path $ArtifactsPayloadRoot $Version) 'YangTools.Revit.addin'
            Label = '本地打包产物'
        }
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

function Test-PayloadAvailable([string]$Version) {
    return $null -ne (Get-VersionSource $Version)
}

function Test-PluginInstalled([string]$Version) {
    $versionRoot = Join-Path $AddinsRoot $Version
    return (Test-Path -LiteralPath (Join-Path $versionRoot 'YangTools\YangTools.Revit.dll')) -and
           (Test-Path -LiteralPath (Join-Path $versionRoot 'YangTools.Revit.addin'))
}

function Get-VersionStatusText([string]$Version) {
    $detected = if (Test-RevitDetected $Version) { '已检测到 Revit' } else { '未检测到 Revit' }
    $source = Get-VersionSource $Version
    $payload = if ($null -ne $source) { "可安装：$($source.Label)" } else { '缺少该年份编译文件' }
    $installed = if (Test-PluginInstalled $Version) { '已安装' } else { '未安装' }
    return "Revit $Version    | $detected | $payload | $installed"
}

function Ensure-RevitClosed {
    if (Test-RevitRunning) {
        throw 'Revit 正在运行。请先关闭全部 Revit 窗口，再执行安装或卸载。'
    }
}

function Install-Version([string]$Version) {
    $source = Get-VersionSource $Version
    if ($null -eq $source) {
        throw "没有找到 Revit $Version 的独立编译文件。"
    }
    $sourcePlugin = $source.PluginRoot
    $sourceDll = $source.Dll
    $sourceManifest = $source.Manifest

    $versionRoot = Join-Path $AddinsRoot $Version
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

        # 重复项目参数清理已在 3.1.30 并入 YangTools；自动清理本项目历史独立插件，避免出现重复按钮。
        $legacyManifest = Join-Path $versionRoot 'RevitParameterDeduplicator.addin'
        $legacyFolder = Join-Path $versionRoot 'RevitParameterDeduplicator'
        $legacyFound = (Test-Path -LiteralPath $legacyManifest) -or (Test-Path -LiteralPath $legacyFolder)
        Remove-Item -LiteralPath $legacyManifest -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $legacyFolder -Recurse -Force -ErrorAction SilentlyContinue
        if ($legacyFound) { Add-Log "Revit $Version：已移除历史独立重复参数清理插件入口。" }

        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $manifestBackup -Force -ErrorAction SilentlyContinue
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

function Uninstall-Version([string]$Version) {
    $versionRoot = Join-Path $AddinsRoot $Version
    foreach ($path in @(
        (Join-Path $versionRoot 'YangTools'),
        (Join-Path $versionRoot 'YangTools.installing'),
        (Join-Path $versionRoot 'YangTools.backup')
    )) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath (Join-Path $versionRoot 'YangTools.Revit.addin') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $versionRoot 'YangTools.Revit.addin.backup') -Force -ErrorAction SilentlyContinue
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'YangTools 快速安装器'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(760, 610)
$form.MinimumSize = New-Object System.Drawing.Size(760, 610)
$form.MaximizeBox = $false
$form.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)
$form.BackColor = [System.Drawing.Color]::FromArgb(246, 248, 250)

$title = New-Object System.Windows.Forms.Label
$title.Text = 'YangTools Revit 插件安装 / 卸载'
$title.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 16, [System.Drawing.FontStyle]::Bold)
$title.AutoSize = $true
$title.Location = New-Object System.Drawing.Point(24, 18)
$form.Controls.Add($title)

$subtitle = New-Object System.Windows.Forms.Label
$subtitle.Text = '当前 Windows 用户安装，无需管理员权限。每个 Revit 年份使用独立编译文件。'
$subtitle.AutoSize = $true
$subtitle.ForeColor = [System.Drawing.Color]::FromArgb(80, 88, 96)
$subtitle.Location = New-Object System.Drawing.Point(27, 55)
$form.Controls.Add($subtitle)

$list = New-Object System.Windows.Forms.CheckedListBox
$list.CheckOnClick = $true
$list.Location = New-Object System.Drawing.Point(28, 88)
$list.Size = New-Object System.Drawing.Size(690, 172)
$list.BorderStyle = 'FixedSingle'
$list.BackColor = [System.Drawing.Color]::White
$form.Controls.Add($list)

$selectAvailable = New-Object System.Windows.Forms.Button
$selectAvailable.Text = '选择可安装版本'
$selectAvailable.Location = New-Object System.Drawing.Point(28, 272)
$selectAvailable.Size = New-Object System.Drawing.Size(120, 32)
$form.Controls.Add($selectAvailable)

$selectInstalled = New-Object System.Windows.Forms.Button
$selectInstalled.Text = '选择已安装版本'
$selectInstalled.Location = New-Object System.Drawing.Point(158, 272)
$selectInstalled.Size = New-Object System.Drawing.Size(120, 32)
$form.Controls.Add($selectInstalled)

$clearSelection = New-Object System.Windows.Forms.Button
$clearSelection.Text = '清除选择'
$clearSelection.Location = New-Object System.Drawing.Point(288, 272)
$clearSelection.Size = New-Object System.Drawing.Size(90, 32)
$form.Controls.Add($clearSelection)

$refreshButton = New-Object System.Windows.Forms.Button
$refreshButton.Text = '刷新状态'
$refreshButton.Location = New-Object System.Drawing.Point(628, 272)
$refreshButton.Size = New-Object System.Drawing.Size(90, 32)
$form.Controls.Add($refreshButton)

$log = New-Object System.Windows.Forms.RichTextBox
$log.Location = New-Object System.Drawing.Point(28, 320)
$log.Size = New-Object System.Drawing.Size(690, 150)
$log.ReadOnly = $true
$log.BackColor = [System.Drawing.Color]::White
$log.BorderStyle = 'FixedSingle'
$log.DetectUrls = $false
$form.Controls.Add($log)

$installButton = New-Object System.Windows.Forms.Button
$installButton.Text = '安装 / 更新'
$installButton.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 10, [System.Drawing.FontStyle]::Bold)
$installButton.Location = New-Object System.Drawing.Point(28, 492)
$installButton.Size = New-Object System.Drawing.Size(140, 42)
$installButton.BackColor = [System.Drawing.Color]::FromArgb(46, 125, 246)
$installButton.ForeColor = [System.Drawing.Color]::White
$installButton.FlatStyle = 'Flat'
$form.Controls.Add($installButton)

$uninstallButton = New-Object System.Windows.Forms.Button
$uninstallButton.Text = '卸载'
$uninstallButton.Location = New-Object System.Drawing.Point(180, 492)
$uninstallButton.Size = New-Object System.Drawing.Size(110, 42)
$form.Controls.Add($uninstallButton)

$openFolderButton = New-Object System.Windows.Forms.Button
$openFolderButton.Text = '打开插件目录'
$openFolderButton.Location = New-Object System.Drawing.Point(302, 492)
$openFolderButton.Size = New-Object System.Drawing.Size(120, 42)
$form.Controls.Add($openFolderButton)

$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Text = '关闭'
$closeButton.Location = New-Object System.Drawing.Point(608, 492)
$closeButton.Size = New-Object System.Drawing.Size(110, 42)
$form.Controls.Add($closeButton)

function Add-Log([string]$Text) {
    $timestamp = Get-Date -Format 'HH:mm:ss'
    $log.AppendText("[$timestamp] $Text`r`n")
    $log.SelectionStart = $log.TextLength
    $log.ScrollToCaret()
    [System.Windows.Forms.Application]::DoEvents()
}


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
    if ($removed.Count -gt 0) {
        Add-Log "已清理旧 AI/摸鱼助手历史、设置和 API Key：$($removed.Count) 项。"
    }
    return @($removed)
}

function Get-SelectedVersions {
    $selected = @()
    foreach ($index in $list.CheckedIndices) {
        $selected += $script:VersionByIndex[[int]$index]
    }
    return @($selected)
}

function Refresh-VersionList([string[]]$Preselect = @()) {
    $checked = if ($Preselect.Count -gt 0) { @($Preselect) } else { @(Get-SelectedVersions) }
    $list.Items.Clear()
    $script:VersionByIndex = @{}
    for ($i = 0; $i -lt $SupportedVersions.Count; $i++) {
        $version = $SupportedVersions[$i]
        [void]$list.Items.Add((Get-VersionStatusText $version), ($version -in $checked))
        $script:VersionByIndex[$i] = $version
    }
}

function Set-ControlsEnabled([bool]$Enabled) {
    foreach ($control in @($installButton, $uninstallButton, $selectAvailable, $selectInstalled, $clearSelection, $refreshButton, $openFolderButton)) {
        $control.Enabled = $Enabled
    }
}

$selectAvailable.Add_Click({
    $versions = @($SupportedVersions | Where-Object { Test-PayloadAvailable $_ -and Test-RevitDetected $_ })
    if ($versions.Count -eq 0) {
        $versions = @($SupportedVersions | Where-Object { Test-PayloadAvailable $_ })
    }
    Refresh-VersionList $versions
})

$selectInstalled.Add_Click({
    Refresh-VersionList @($SupportedVersions | Where-Object { Test-PluginInstalled $_ })
})

$clearSelection.Add_Click({ Refresh-VersionList @('__none__') })
$refreshButton.Add_Click({ Refresh-VersionList })

$installButton.Add_Click({
    try {
        Ensure-RevitClosed
        [void](Remove-LegacyAssistantData)
        $versions = @(Get-SelectedVersions)
        if ($versions.Count -eq 0) { throw '请至少选择一个 Revit 版本。' }

        $available = @()
        $skipped = @()
        foreach ($version in $versions) {
            if (Test-PayloadAvailable $version) { $available += $version }
            else { $skipped += $version }
        }
        if ($available.Count -eq 0) {
            throw "所选版本均没有独立编译文件：$($versions -join '、')。请先编译对应年份。"
        }

        Set-ControlsEnabled $false
        foreach ($version in $skipped) {
            Add-Log "跳过 Revit $version：没有该年份独立编译文件。"
        }
        foreach ($version in $available) {
            $source = Get-VersionSource $version
            Add-Log "正在安装 Revit $version（$($source.Label)）..."
            Install-Version $version
            Add-Log "Revit $version 安装完成。"
        }
        Refresh-VersionList $available
        $message = 'YangTools 安装/更新完成。现在可以启动 Revit。'
        if ($skipped.Count -gt 0) {
            $message += "`r`n`r`n已跳过无编译文件的版本：$($skipped -join '、')。"
        }
        [System.Windows.Forms.MessageBox]::Show($message, 'YangTools', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Log "失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '安装失败', 'OK', 'Error') | Out-Null
    }
    finally { Set-ControlsEnabled $true }
})

$uninstallButton.Add_Click({
    try {
        Ensure-RevitClosed
        $versions = @(Get-SelectedVersions)
        if ($versions.Count -eq 0) { throw '请至少选择一个 Revit 版本。' }
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "将卸载 Revit $($versions -join '、') 的 YangTools，并清理已删除 AI/摸鱼助手的历史、设置和 API Key。其他仍在使用的个人设置会保留。是否继续？",
            '确认卸载', 'YesNo', 'Question')
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { return }
        Set-ControlsEnabled $false
        [void](Remove-LegacyAssistantData)
        foreach ($version in $versions) {
            Add-Log "正在卸载 Revit $version ..."
            Uninstall-Version $version
            Add-Log "Revit $version 卸载完成。"
        }
        Refresh-VersionList $versions
        [System.Windows.Forms.MessageBox]::Show('YangTools 卸载完成；旧 AI/摸鱼助手数据已清理，其他仍在使用的个人设置已保留。', 'YangTools', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Log "失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '卸载失败', 'OK', 'Error') | Out-Null
    }
    finally { Set-ControlsEnabled $true }
})

$openFolderButton.Add_Click({
    New-Item -ItemType Directory -Force -Path $AddinsRoot | Out-Null
    Start-Process explorer.exe -ArgumentList ('"' + $AddinsRoot + '"')
})
$closeButton.Add_Click({ $form.Close() })

$initial = @($SupportedVersions | Where-Object { Test-PayloadAvailable $_ -and Test-RevitDetected $_ })
if ($initial.Count -eq 0) { $initial = @($SupportedVersions | Where-Object { Test-PayloadAvailable $_ }) }
Refresh-VersionList $initial
Add-Log "安装目录：$AddinsRoot"
Add-Log '可直接使用正式 payload，也可识别本项目 bin\x64\年份 的本地编译输出。'
if (-not (Test-Path -LiteralPath $PayloadRoot)) {
    Add-Log '当前为源码模式：若已编译，将直接使用 bin\x64\年份 的输出。'
}
[void]$form.ShowDialog()
