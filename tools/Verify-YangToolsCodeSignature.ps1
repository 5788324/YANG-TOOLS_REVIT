[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string[]]$Files,
    [switch]$AllowUntrustedSelfSigned
)

$ErrorActionPreference = 'Stop'
$failed = $false

foreach ($file in $Files) {
    $full = [System.IO.Path]::GetFullPath($file)
    if (-not (Test-Path $full -PathType Leaf)) {
        Write-Error "File not found: $full"
        $failed = $true
        continue
    }

    $signature = Get-AuthenticodeSignature -FilePath $full
    $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { '<none>' }
    Write-Host "File:    $full"
    Write-Host "Status:  $($signature.Status)"
    Write-Host "Signer:  $subject"
    Write-Host "Message: $($signature.StatusMessage)"
    Write-Host ''

    if ($signature.Status -eq 'Valid') { continue }

    if ($AllowUntrustedSelfSigned -and $signature.SignerCertificate -and $signature.SignerCertificate.Subject -match 'CN=Yang') {
        Write-Warning "Signature exists but is not trusted by this machine: $full"
        continue
    }

    $failed = $true
}

if ($failed) { throw 'One or more YangTools signatures failed verification.' }
Write-Host 'Signature verification completed.' -ForegroundColor Green
