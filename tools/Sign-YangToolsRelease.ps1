[CmdletBinding(DefaultParameterSetName='Thumbprint')]
param(
    [Parameter(Mandatory=$true)]
    [string[]]$Files,

    [Parameter(Mandatory=$true, ParameterSetName='Thumbprint')]
    [string]$CertificateThumbprint,

    [Parameter(Mandatory=$true, ParameterSetName='Pfx')]
    [string]$CertificatePath,

    [Parameter(ParameterSetName='Pfx')]
    [string]$PfxPasswordEnvironmentVariable = 'YANGTOOLS_SIGN_PFX_PASSWORD',

    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipTimestamp,
    [switch]$AllowUntrustedSelfSigned
)

$ErrorActionPreference = 'Stop'

function Find-SignTool {
    $candidates = @()
    if ($env:ProgramFiles -and (Test-Path "$env:ProgramFiles\Windows Kits\10\bin")) {
        $candidates += Get-ChildItem "$env:ProgramFiles\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' }
    }
    ${pf86} = ${env:ProgramFiles(x86)}
    if (${pf86} -and (Test-Path "${pf86}\Windows Kits\10\bin")) {
        $candidates += Get-ChildItem "${pf86}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' }
    }
    $tool = $candidates | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw 'signtool.exe not found. Install the Windows SDK signing tools.' }
    return $tool.FullName
}

$signTool = Find-SignTool
Write-Host "signtool: $signTool"

foreach ($file in $Files) {
    $full = [System.IO.Path]::GetFullPath($file)
    if (-not (Test-Path $full -PathType Leaf)) { throw "File not found: $full" }

    $args = @('sign', '/fd', 'SHA256')
    if ($PSCmdlet.ParameterSetName -eq 'Thumbprint') {
        $args += @('/sha1', $CertificateThumbprint, '/s', 'My')
    }
    else {
        $pfx = [System.IO.Path]::GetFullPath($CertificatePath)
        if (-not (Test-Path $pfx -PathType Leaf)) { throw "PFX not found: $pfx" }
        $password = [Environment]::GetEnvironmentVariable($PfxPasswordEnvironmentVariable)
        if ([string]::IsNullOrEmpty($password)) {
            throw "PFX password environment variable '$PfxPasswordEnvironmentVariable' is empty. Passwords are never stored in this repository."
        }
        $args += @('/f', $pfx, '/p', $password)
    }

    if (-not $SkipTimestamp) {
        $args += @('/tr', $TimestampUrl, '/td', 'SHA256')
    }
    $args += $full

    Write-Host "Signing: $full" -ForegroundColor Cyan
    & $signTool @args
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed for $full (exit $LASTEXITCODE)" }

    if ($AllowUntrustedSelfSigned) {
        $signature = Get-AuthenticodeSignature -FilePath $full
        $subject = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { '' }
        if (-not $signature.SignerCertificate -or $subject -notmatch 'CN=Yang') {
            throw "Expected a Yang self-signed certificate after signing: $full"
        }
        if ($signature.Status -eq 'NotSigned' -or $signature.Status -eq 'HashMismatch') {
            throw "Self-signed Authenticode signature is missing or invalid for ${full}: $($signature.Status)"
        }
        Write-Warning "Self-signed signature detected. Trust status is '$($signature.Status)'; this is acceptable only for local/internal signing."
    }
    else {
        & $signTool verify /pa /v $full
        if ($LASTEXITCODE -ne 0) { throw "signtool verify failed for $full (exit $LASTEXITCODE)" }
    }
}

Write-Host 'All requested YangTools files were signed and verified.' -ForegroundColor Green
