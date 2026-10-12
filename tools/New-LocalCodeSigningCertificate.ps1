[CmdletBinding()]
param(
    [string]$Subject = 'CN=Yang, O=YangTools',
    [int]$ValidYears = 5,
    [string]$ExportPublicCer = ''
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $PSVersionTable.PSVersion.Major -ge 6) {
    throw 'Code-signing certificates can only be created on Windows.'
}

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy NonExportable `
    -NotAfter (Get-Date).AddYears($ValidYears)

Write-Host "Created local code-signing certificate:" -ForegroundColor Green
Write-Host "  Subject:    $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Expires:    $($cert.NotAfter)"
Write-Host ''
Write-Host 'Important: this self-signed certificate is intended for local/internal signing.' -ForegroundColor Yellow
Write-Host 'Public recipients will not automatically trust it. Use a trusted CA certificate for public releases.' -ForegroundColor Yellow

if (-not [string]::IsNullOrWhiteSpace($ExportPublicCer)) {
    $target = [System.IO.Path]::GetFullPath($ExportPublicCer)
    $dir = Split-Path -Parent $target
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Export-Certificate -Cert $cert -FilePath $target | Out-Null
    Write-Host "Public certificate exported to: $target"
}

$cert.Thumbprint
