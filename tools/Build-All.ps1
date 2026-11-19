[CmdletBinding()]
param(
    [string[]]$Versions = @('2021','2022','2023','2024','2025','2026','2027'),
    [switch]$NoRestore,
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'
$Artifacts = Join-Path $ProjectRoot 'artifacts\payload'
$Manifest = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.addin'

if ($Deploy) {
    throw '自动部署模式已禁用。请先隔离编译，然后通过中文安装器选择明确的 Revit 年份安装（保留上一版备份）。'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '找不到 dotnet。请安装 .NET 8 SDK、.NET 10 SDK 和 .NET Framework 4.8 Developer Pack。'
}

Remove-Item $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
foreach ($version in $Versions) {
    Write-Host "=== 构建 Revit $version ===" -ForegroundColor Cyan
    $arguments = @('build', $Project, '-c', $version, '-p:Platform=x64', '-p:DeployAfterBuild=false')
    if ($NoRestore) { $arguments += '--no-restore' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Revit $version 构建失败。" }

    $output = Join-Path $ProjectRoot "src\YangTools.Revit\bin\x64\$version"
    $dll = Join-Path $output 'YangTools.Revit.dll'
    if (-not (Test-Path $dll)) { throw "Revit $version 输出中缺少 YangTools.Revit.dll。" }

    $targetRoot = Join-Path $Artifacts $version
    $targetPlugin = Join-Path $targetRoot 'YangTools'
    New-Item -ItemType Directory -Force -Path $targetPlugin | Out-Null
    Get-ChildItem $output -File -Recurse | Where-Object {
        $_.Extension -ne '.pdb' -and
        $_.Name -notin @('RevitAPI.dll', 'RevitAPIUI.dll')
    } | ForEach-Object {
        $relative = $_.FullName.Substring($output.Length).TrimStart('\')
        $destination = Join-Path $targetPlugin $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item $_.FullName $destination -Force
    }
    $artifactManifest = Join-Path $targetRoot 'YangTools.Revit.addin'
    Copy-Item $Manifest $artifactManifest -Force

}

Write-Host "全部构建完成：$Artifacts" -ForegroundColor Green
