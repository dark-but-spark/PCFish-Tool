param(
    [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
$name = "PCFish-Tool-v$version"
$stage = Join-Path $root (Join-Path $OutDir $name)
$zip = Join-Path $root (Join-Path $OutDir "$name.zip")
$outPath = Join-Path $root $OutDir

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

foreach ($dir in @("src", "tools", "docs")) {
    Copy-Item -Path (Join-Path $root $dir) -Destination $stage -Recurse -Force
}

foreach ($file in @(
    "README.md",
    "LICENSE",
    "RELEASE_NOTES.md",
    "CHANGELOG.md",
    "VERSION",
    "run.cmd"
)) {
    Copy-Item -Path (Join-Path $root $file) -Destination $stage -Force
}

Get-ChildItem -Path $stage -Recurse -Directory -Filter "__pycache__" -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force
Get-ChildItem -Path $stage -Recurse -File -Filter "*.pyc" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$sums = Join-Path $outPath "SHA256SUMS.txt"
"$hash  $name.zip" | Set-Content -Path $sums -Encoding ASCII

Write-Host "Built: $zip"
Get-Content $sums
