#Requires -Version 5.1
<#
.SYNOPSIS
  Пишет .godot/export_credentials.cfg: один keystore на debug и release.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$keystore = Join-Path $root "magicalball-release.keystore"
if (-not (Test-Path $keystore)) {
    Write-Host "ERROR: missing $keystore" -ForegroundColor Red
    exit 1
}

$godot = Join-Path $root ".godot"
New-Item -ItemType Directory -Force -Path $godot | Out-Null
$unix = ($keystore -replace '\\', '/')
$cfg = @"
[preset.0]

script_encryption_key=""

[preset.0.options]

keystore/debug="$unix"
keystore/debug_user="magicalball"
keystore/debug_password="123456"
keystore/release="$unix"
keystore/release_user="magicalball"
keystore/release_password="123456"
"@
$path = Join-Path $godot "export_credentials.cfg"
[IO.File]::WriteAllText($path, $cfg.Replace("`n", "`r`n"), [Text.UTF8Encoding]::new($false))
Write-Host "OK: $path"
