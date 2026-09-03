# Build a ready-to-upload tree for WinSCP -> VPS /opt/magicalball/
# From repo root on Windows:
#   .\deploy\prepare-winscp.ps1
#
# Output: deploy\winscp-upload\ - upload THIS folder's CONTENTS into /opt/magicalball/
param()

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Stage = Join-Path $Root "deploy\winscp-upload"

function Copy-TreeFiltered {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )
    if (-not (Test-Path $Source)) {
        Write-Error "Missing: $Source"
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & robocopy $Source $Destination /E /NFL /NDL /NJH /NJS /NP `
        /XD .venv __pycache__ .pytest_cache .mypy_cache .ruff_cache .git data tests `
        /XF .env .env.* *.json *.pyc | Out-Null
    $rc = $LASTEXITCODE
    if ($rc -ge 8) {
        Write-Error "robocopy failed ($rc): $Source -> $Destination"
    }
}

Write-Host "=== MagicalBall WinSCP pack ==="
Write-Host "Repo: $Root"

if (Test-Path $Stage) {
    Remove-Item -Recurse -Force $Stage
}
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

foreach ($name in @("docker-compose.yml", ".dockerignore")) {
    $src = Join-Path $Root $name
    if (-not (Test-Path $src)) { Write-Error "Missing: $src" }
    Copy-Item -Force $src (Join-Path $Stage $name)
}

Copy-TreeFiltered -Source (Join-Path $Root "proxy") -Destination (Join-Path $Stage "proxy")

$deployDst = Join-Path $Stage "deploy"
New-Item -ItemType Directory -Force -Path (Join-Path $deployDst "backups") | Out-Null
Get-ChildItem -Path $PSScriptRoot -File | ForEach-Object {
    if ($_.Name -eq "prepare-winscp.ps1") { return }
    Copy-Item -Force $_.FullName (Join-Path $deployDst $_.Name)
}

$stageSize = (Get-ChildItem -Recurse -File $Stage | Measure-Object -Property Length -Sum).Sum
Write-Host ""
Write-Host ("OK - staged: {0} ({1:N1} MB)" -f $Stage, ($stageSize / 1MB))
Write-Host ""
Write-Host "WinSCP:"
Write-Host "  1) VPS:  mkdir -p /opt/magicalball"
Write-Host "  2) Local folder:  $Stage"
Write-Host "  3) Upload CONTENTS of winscp-upload into /opt/magicalball/"
Write-Host ""
Write-Host "Then on VPS:"
Write-Host "  cd /opt/magicalball"
Write-Host "  cp deploy/env.example .env"
Write-Host "  nano .env   # GIGACHAT_CREDENTIALS=...  VPS_IP=147.45.173.26  API_PORT=18437"
Write-Host "  chmod +x deploy/*.sh"
Write-Host "  bash deploy/check-env.sh"
Write-Host "  bash deploy/install-on-vps.sh"

exit 0
