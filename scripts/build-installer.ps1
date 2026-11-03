param(
    [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
$outPath = Join-Path $root $OutDir
$exeDir = Join-Path $root "dist-exe"
$workDir = Join-Path $root "build-exe"
$installerSource = Join-Path $root "installer\Installer.cs"
$installerGenerated = Join-Path $root "installer\Installer.g.cs"
$js = Join-Path $root "src\pcfish_autogroup.js"
$setup = Join-Path $outPath ("PCFishTool-Setup-v{0}.exe" -f $version)

New-Item -ItemType Directory -Force -Path $outPath, $exeDir, $workDir | Out-Null

Write-Host "Building PCFishTool.exe with PyInstaller..."
$ErrorActionPreference = "Continue"
& python -m PyInstaller --noconfirm --onefile --console --name PCFishTool `
    --distpath $exeDir --workpath $workDir --specpath $workDir `
    (Join-Path $root "tools\attach.py") 2>&1 | ForEach-Object { Write-Host $_ }
$pyCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($pyCode -ne 0) { throw "PyInstaller failed" }

$toolExe = Join-Path $exeDir "PCFishTool.exe"
if (-not (Test-Path $toolExe)) { throw "Missing $toolExe" }
Copy-Item -Path $js -Destination (Join-Path $exeDir "pcfish_autogroup.js") -Force

Write-Host "Generating installer source..."
$src = Get-Content -Path $installerSource -Raw -Encoding UTF8
$src = $src.Replace("__VERSION__", $version)
Set-Content -Path $installerGenerated -Value $src -Encoding UTF8

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

Write-Host "Compiling installer..."
$ErrorActionPreference = "Continue"
& $csc /nologo /target:winexe /platform:x64 /out:$setup `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /resource:$toolExe,PCFishTool.exe `
    /resource:$js,pcfish_autogroup.js `
    $installerGenerated 2>&1 | ForEach-Object { Write-Host $_ }
$cscCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($cscCode -ne 0) { throw "csc failed" }

Write-Host "Built: $setup"

# Rebuild SHA256SUMS for all release assets that exist
$assets = @(
    ("PCFish-Tool-v{0}.zip" -f $version),
    ("PCFishTool-Setup-v{0}.exe" -f $version)
)
$lines = @()
foreach ($name in $assets) {
    $path = Join-Path $outPath $name
    if (Test-Path $path) {
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
        $lines += "$hash  $name"
    }
}
$lines | Set-Content -Path (Join-Path $outPath "SHA256SUMS.txt") -Encoding ASCII
Get-Content (Join-Path $outPath "SHA256SUMS.txt")
