# Compiles source in a Windows SDK environment WITHOUT modifying Revit add-ins or user data.
# This script is an execution entrypoint, not evidence that the build has been run.
[CmdletBinding()]
param([string[]]$Versions = @('2024', '2025'), [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\YangTools.Revit\YangTools.Revit.csproj'
if (-not (Test-Path -LiteralPath $project)) { throw "找不到 Revit 项目文件：$project" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '缺少 dotnet SDK，构建未执行。'
}
$allowed = @('2021','2022','2023','2024','2025','2026','2027')
foreach ($version in $Versions) {
    if ($version -notin $allowed) { throw "不支持的 Revit 编译配置：$version" }
}
$logs = Join-Path $root 'artifacts\rc3c-build-evidence'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$all = @()
foreach ($version in $Versions) {
    $log = Join-Path $logs "dotnet-$version.log"
    $args = @('build', $project, '--configuration', $version, '-p:Platform=x64',
              '-p:DeployAfterBuild=false', '-p:RunPostBuildEvent=Never', '--nologo')
    if ($NoRestore) { $args += '--no-restore' }
    & dotnet @args *> $log
    $exit = $LASTEXITCODE
    $dll = Join-Path $root "src\YangTools.Revit\bin\x64\$version\YangTools.Revit.dll"
    $exists = $exit -eq 0 -and (Test-Path -LiteralPath $dll)
    $all += [PSCustomObject]@{
        revit_version = $version
        dotnet_exit_code = $exit
        dll_exists = [bool]$exists
        dll_sha256 = if ($exists) { (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash } else { $null }
        build_log = $log
    }
    if (-not $exists) { break }
}
$evidence = Join-Path $logs 'build-results.json'
$all | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $evidence -Encoding UTF8
Write-Host "编译记录：$evidence"
if (@($all | Where-Object { $_.dotnet_exit_code -ne 0 -or -not $_.dll_exists }).Count -ne 0 -or $all.Count -ne $Versions.Count) {
    throw '至少一项 C# 编译未通过，详见各版本日志。未进行任何 Revit 部署。'
}
Write-Host '所选版本源码编译 PASS；未进行 Revit 加载或部署。'
