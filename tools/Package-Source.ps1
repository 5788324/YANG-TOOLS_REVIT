[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ProjectFile = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'

if (-not (Test-Path -LiteralPath $ProjectFile)) {
    throw "Project file not found: $ProjectFile"
}

[xml]$ProjectXml = Get-Content -LiteralPath $ProjectFile -Raw -Encoding UTF8
$VersionNodes = @($ProjectXml.SelectNodes('/Project/PropertyGroup/Version'))
if ($VersionNodes.Count -ne 1) {
    throw "Expected exactly one Version node, found $($VersionNodes.Count)."
}
$Version = [string]$VersionNodes[0].InnerText
$Date = Get-Date -Format 'yyyyMMdd'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot 'artifacts\source-package'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$Stage = Join-Path $OutputDirectory "YangTools-Source-$Version-$Date"
$Zip = "$Stage.zip"
Remove-Item -LiteralPath $Stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $Zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

$ExcludedDirectories = @(
    '.git', '.vs', '.vscode', '.gemini', '.codex', '.agents',
    'bin', 'obj', 'artifacts', 'deploy', '__pycache__', 'payload'
)
$ExcludedExtensions = @('.zip', '.pyc', '.pdb', '.cache', '.log')

$RootLength = $ProjectRoot.TrimEnd('\').Length
Get-ChildItem -LiteralPath $ProjectRoot -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($RootLength).TrimStart('\')
    $parts = $relative -split '[\\/]'
    if ($parts | Where-Object { $_ -in $ExcludedDirectories }) { return }
    if ($_.Extension.ToLowerInvariant() -in $ExcludedExtensions) { return }

    $destination = Join-Path $Stage $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}

$RequiredInstallerNames = @(
    'Install.ps1', 'Uninstall.ps1',
    'YangTools安装器.ps1', 'YangTools安装器.cmd',
    '安装.cmd', '卸载.cmd'
)
foreach ($name in $RequiredInstallerNames) {
    $path = Join-Path (Join-Path $Stage 'installer') $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Source package staging lost installer filename: $name"
    }
}

Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Zip -CompressionLevel Optimal
$Hash = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash
Write-Host "Source package: $Zip" -ForegroundColor Green
Write-Host "SHA-256: $Hash" -ForegroundColor Cyan
