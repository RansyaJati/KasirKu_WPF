<#
.SYNOPSIS
    Membuat hash password PBKDF2-SHA256 yang kompatibel 100% dengan
    implementasi AuthService di KasirKu.Shared.Services.AuthService.

.DESCRIPTION
    Algoritma yang dipakai KasirKu:
      - Salt       : 16 byte acak (Base64)
      - Algoritma  : PBKDF2 (Rfc2898DeriveBytes)
      - Hash       : SHA-256
      - Iterasi    : 100.000
      - Output     : 32 byte (Base64)

    Gunakan output script ini untuk:
      1. Menyiapkan akun hasil migrasi database lama.
      2. Mengisi variabel @temp_hash dan @temp_salt di 04_etl_template.sql.
      3. Membuat akun admin darurat tanpa lewat UI.

.PARAMETER Password
    Password plain text yang akan di-hash. Minimal 4 karakter.

.PARAMETER Username
    (Opsional) Username untuk menghasilkan potongan query SQL siap pakai.

.PARAMETER Role
    (Opsional) Role akun: 'Kasir' atau 'Manager'. Default: 'Kasir'.

.PARAMETER NamaLengkap
    (Opsional) Nama tampilan user.

.EXAMPLE
    pwsh -File New-KasirKuPasswordHash.ps1 -Password "GantiSaya123!"

.EXAMPLE
    pwsh -File New-KasirKuPasswordHash.ps1 -Password "admin123" -Username "admin" -Role "Manager" -NamaLengkap "Administrator"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Password,

    [Parameter(Mandatory = $false)]
    [string]$Username,

    [Parameter(Mandatory = $false)]
    [ValidateSet('Kasir', 'Manager')]
    [string]$Role = 'Kasir',

    [Parameter(Mandatory = $false)]
    [string]$NamaLengkap = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Password.Length -lt 4) {
    Write-Error "Password terlalu pendek. KasirKu mensyaratkan minimal 4 karakter."
    exit 1
}

# 1. Salt 16 byte acak
$saltBytes = New-Object byte[] 16
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $rng.GetBytes($saltBytes)
} finally {
    $rng.Dispose()
}
$saltBase64 = [Convert]::ToBase64String($saltBytes)

# 2. PBKDF2-SHA256, 100.000 iterasi, 32 byte hash
# Kompatibel dengan Windows PowerShell 5.1 (.NET Framework) maupun PowerShell 7+ (.NET Core).
$hashBytes = $null
$pbkdf2Method = [System.Security.Cryptography.Rfc2898DeriveBytes].GetMethod(
    'Pbkdf2',
    [System.Reflection.BindingFlags]'Public, Static',
    $null,
    @([byte[]], [byte[]], [int], [System.Security.Cryptography.HashAlgorithmName], [int]),
    $null
)

if ($pbkdf2Method -ne $null) {
    # .NET 7+ / .NET 10 bawaan PowerShell 7+
    $passwordBytes = [System.Text.Encoding]::UTF8.GetBytes($Password)
    $hashBytes = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
        $passwordBytes,
        $saltBytes,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        32
    )
} else {
    # .NET Framework (Windows PowerShell 5.1 bawaan Windows)
    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $Password,
        $saltBytes,
        100000,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256
    )
    try {
        $hashBytes = $kdf.GetBytes(32)
    } finally {
        $kdf.Dispose()
    }
}
$hashBase64 = [Convert]::ToBase64String($hashBytes)

# 3. Output
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  KasirKu - PBKDF2 Password Hash Generator" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Password      : $Password"
Write-Host "Salt (Base64) : $saltBase64"
Write-Host "Hash (Base64) : $hashBase64"
Write-Host "Iterasi       : 100,000"
Write-Host "Hash Alg      : SHA-256"
Write-Host "Output Size   : 32 bytes"
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "-- [1] Untuk variabel di 04_etl_template.sql:" -ForegroundColor Yellow
$lineHash = 'SET @temp_hash = ' + "'" + $hashBase64 + "';"
$lineSalt = 'SET @temp_salt = ' + "'" + $saltBase64 + "';"
Write-Host $lineHash
Write-Host $lineSalt

if (-not [string]::IsNullOrWhiteSpace($Username)) {
    $u = $Username.Trim().ToLowerInvariant()
    $nama = if ([string]::IsNullOrWhiteSpace($NamaLengkap)) { $Username.Trim() } else { $NamaLengkap.Trim() }

    Write-Host ""
    Write-Host "-- [2] Query INSERT langsung ke kasirku.users:" -ForegroundColor Yellow
    Write-Host "INSERT INTO kasirku.users (username, nama_lengkap, role, password_hash, salt, aktif)"
    $lineVal = "VALUES ('" + $u + "', '" + $nama + "', '" + $Role + "', '" + $hashBase64 + "', '" + $saltBase64 + "', TRUE)"
    Write-Host $lineVal
    Write-Host "ON DUPLICATE KEY UPDATE"
    Write-Host "    password_hash = VALUES(password_hash),"
    Write-Host "    salt          = VALUES(salt);"
}

Write-Host ""
