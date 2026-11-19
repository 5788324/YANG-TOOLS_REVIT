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
. (Join-Path $PSScriptRoot 'Assert-TargetRevitClosed.ps1')
$script:VersionByIndex = @{}


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
    # 只清理当前未完成的 staging；旧安装目录/manifest 在新 payload 校验之前不得删除。
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction Stop }
    $hadTarget = Test-Path -LiteralPath $target
    $hadManifest = Test-Path -LiteralPath $manifest
    $movedTarget = $false
    $movedManifest = $false
    if (($hadTarget -and -not $hadManifest) -or (-not $hadTarget -and $hadManifest)) {
        throw "Revit $Version 插件文件和 manifest 状态不一致，已停止安装；请检查现有文件。"
    }
    if (-not $hadTarget -and ((Test-Path -LiteralPath $backup) -or (Test-Path -LiteralPath $manifestBackup))) {
        throw "Revit $Version 尚有上一版备份但当前插件目录不存在，请先检查或恢复备份。"
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
        $legacyManifest = Join-Path $versionRoot 'RevitParameterDeduplicator.addin'
        if (Test-Path -LiteralPath $legacyManifest) {
            Add-Log "Revit $Version：检测到旧重复参数独立插件；为保护现有插件未自动删除。如有重复按钮请自行检查。"
        }
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

$rollbackButton = New-Object System.Windows.Forms.Button
$rollbackButton.Text = '恢复上一版'
$rollbackButton.Location = New-Object System.Drawing.Point(432, 492)
$rollbackButton.Size = New-Object System.Drawing.Size(138, 42)
$form.Controls.Add($rollbackButton)

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
    foreach ($control in @($installButton, $uninstallButton, $selectAvailable, $selectInstalled, $clearSelection, $refreshButton, $openFolderButton, $rollbackButton)) {
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

        Assert-TargetRevitClosed -Versions $available
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
        $versions = @(Get-SelectedVersions)
        if ($versions.Count -eq 0) { throw '请至少选择一个 Revit 版本。' }
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "将卸载 Revit $($versions -join '、') 的 YangTools。已有个人设置与历史数据会保留。是否继续？",
            '确认卸载', 'YesNo', 'Question')
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { return }
        Assert-TargetRevitClosed -Versions $versions
        Set-ControlsEnabled $false
        foreach ($version in $versions) {
            Add-Log "正在卸载 Revit $version ..."
            Uninstall-Version $version
            Add-Log "Revit $version 卸载完成。"
        }
        Refresh-VersionList $versions
        [System.Windows.Forms.MessageBox]::Show('YangTools 卸载完成；个人设置和历史数据均已保留。', 'YangTools', 'OK', 'Information') | Out-Null
    }
    catch {
        Add-Log "失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '卸载失败', 'OK', 'Error') | Out-Null
    }
    finally { Set-ControlsEnabled $true }
})

$rollbackButton.Add_Click({
    try {
        $versions = @(Get-SelectedVersions)
        if ($versions.Count -ne 1) { throw '每次请选择一个 Revit 年份恢复上一版。' }
        $version = $versions[0]
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "恢复 Revit $version 的上一版 YangTools？当前版本将转存到备份位置。", '确认回退', 'YesNo', 'Question')
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { return }
        Assert-TargetRevitClosed -Versions @($version)
        Set-ControlsEnabled $false
        & (Join-Path $PSScriptRoot 'Rollback.ps1') -Version $version
        Add-Log "Revit $version 已恢复上一版。"
        Refresh-VersionList $versions
    }
    catch {
        Add-Log "恢复失败：$($_.Exception.Message)"
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, '回退失败', 'OK', 'Error') | Out-Null
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
