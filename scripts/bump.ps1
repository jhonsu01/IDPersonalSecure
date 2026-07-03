<#
.SYNOPSIS
    Sube la versión (SemVer), crea el commit + tag y lo publica.
    Al empujar el tag `vX.Y.Z`, GitHub Actions compila APK + MSI y publica la release,
    borrando automáticamente la release anterior.

.EXAMPLE
    pwsh scripts/bump.ps1 -Part patch     # 0.1.0 -> 0.1.1
    pwsh scripts/bump.ps1 -Part minor     # 0.1.1 -> 0.2.0
    pwsh scripts/bump.ps1 -Part major     # 0.2.0 -> 1.0.0
#>
param(
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Part = 'patch'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $root 'VERSION'

$current = (Get-Content $versionFile -Raw).Trim()
$parts = $current.Split('.')
[int]$maj = $parts[0]; [int]$min = $parts[1]; [int]$pat = $parts[2]

switch ($Part) {
    'major' { $maj++; $min = 0; $pat = 0 }
    'minor' { $min++; $pat = 0 }
    'patch' { $pat++ }
}
$new = "$maj.$min.$pat"
Set-Content -Path $versionFile -Value $new -NoNewline -Encoding utf8

Write-Host "Versión: $current -> $new"

git -C $root add VERSION
git -C $root commit -m "chore: bump version to $new"
git -C $root tag "v$new"
git -C $root push origin HEAD
git -C $root push origin "v$new"

Write-Host "Tag v$new publicado. GitHub Actions generará la release." -ForegroundColor Green
