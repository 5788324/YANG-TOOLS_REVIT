[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'

$YangToolsUserDataRoot = Join-Path $env:APPDATA 'YangTools'
$LegacyAssistantDirectory = Join-Path $YangToolsUserDataRoot 'Assistant'
$LegacyAssistantFiles = @(
    (Join-Path $YangToolsUserDataRoot 'copilot_settings.json'),
    (Join-Path $YangToolsUserDataRoot 'copilot_history.json'),
    (Join-Path $YangToolsUserDataRoot 'moyu_settings.json'),
    (Join-Path $YangToolsUserDataRoot 'moyu_history.json')
)

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
    return @($removed)
}


if (Get-Process -Name Revit -ErrorAction SilentlyContinue) {
    throw 'Revit is running. Close every Revit process before building and deploying 2024/2025.'
}

$removedAssistantData = @(Remove-LegacyAssistantData)
if ($removedAssistantData.Count -gt 0) {
    Write-Host "[YangTools] Removed retired Assistant user data: $($removedAssistantData.Count) item(s)." -ForegroundColor DarkCyan
}

if (-not (Test-Path -LiteralPath $Project)) {
    throw "Project file not found: $Project"
}

[xml]$ProjectXml = Get-Content -LiteralPath $Project -Raw -Encoding UTF8
$VersionNodes = @($ProjectXml.SelectNodes('/Project/PropertyGroup/Version'))
if ($VersionNodes.Count -ne 1) {
    throw "Expected exactly one Version node, found $($VersionNodes.Count)."
}
$ExpectedVersion = [string]$VersionNodes[0].InnerText

$Results = @()
foreach ($RevitVersion in @('2024', '2025')) {
    $AddinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
    $DeployFolder = Join-Path $AddinRoot 'YangTools'
    $Manifest = Join-Path $AddinRoot 'YangTools.Revit.addin'

    Remove-Item -LiteralPath $DeployFolder -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $Manifest -Force -ErrorAction SilentlyContinue

    Write-Host "[YangTools] Build and deploy Revit $RevitVersion..." -ForegroundColor Cyan
    & dotnet build $Project `
        -c $RevitVersion `
        -p:Platform=x64 `
        -p:DeployAfterBuild=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build/deploy failed for Revit $RevitVersion. Exit code: $LASTEXITCODE"
    }

    $OutputDll = Join-Path $ProjectRoot "src\YangTools.Revit\bin\x64\$RevitVersion\YangTools.Revit.dll"
    $DeployDll = Join-Path $DeployFolder 'YangTools.Revit.dll'
    if (-not (Test-Path -LiteralPath $OutputDll)) {
        throw "Build output DLL missing for Revit ${RevitVersion}: $OutputDll"
    }
    if (-not (Test-Path -LiteralPath $DeployDll)) {
        throw "Deployed DLL missing for Revit ${RevitVersion}: $DeployDll"
    }
    if (-not (Test-Path -LiteralPath $Manifest)) {
        throw "Add-in manifest missing for Revit ${RevitVersion}: $Manifest"
    }

    $Info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($DeployDll)
    if ($Info.ProductVersion -ne $ExpectedVersion) {
        throw "Deployed ProductVersion mismatch for Revit $RevitVersion. Actual: $($Info.ProductVersion); expected: $ExpectedVersion"
    }

    $Results += [PSCustomObject]@{
        RevitVersion  = $RevitVersion
        FileVersion   = $Info.FileVersion
        ProductVersion = $Info.ProductVersion
        DllPath       = $DeployDll
        Sha256        = (Get-FileHash -LiteralPath $DeployDll -Algorithm SHA256).Hash
        Manifest      = $Manifest
    }
}

Write-Host ''
Write-Host '[YangTools] Revit 2024 and 2025 were both built and deployed.' -ForegroundColor Green
$Results | Format-Table -AutoSize
