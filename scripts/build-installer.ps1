param(
    [string]$OutDir = "dist",
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($GameDir)) {
    $GameDir = Join-Path $root ".dev\PC FISH"
}
$GameDir = (Resolve-Path $GameDir).Path

$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
$outPath = Join-Path $root $OutDir
$installerSource = Join-Path $root "installer\Installer.cs"
$installerGenerated = Join-Path $root "installer\Installer.g.cs"
$manifest = Join-Path $root "installer\app.manifest"
$payloadDir = Join-Path $root "installer\payload"
$payloadZip = Join-Path $root "installer\payload.zip"
$modProject = Join-Path $root "mods\PCFishTool"
$modDll = Join-Path $modProject "bin\Release\PCFishTool.dll"

New-Item -ItemType Directory -Force -Path $outPath | Out-Null

Write-Host "Building mod..."
$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
$ErrorActionPreference = "Continue"
& $dotnet build $modProject -c Release "-p:GameDir=$GameDir" 2>&1 | ForEach-Object { Write-Host $_ }
$dotnetCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($dotnetCode -ne 0) { throw "dotnet build failed" }
if (-not (Test-Path $modDll)) { throw "Missing $modDll" }

Write-Host "Building payload..."
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
New-Item -ItemType Directory -Force -Path $payloadDir, (Join-Path $payloadDir "Mods") | Out-Null

Copy-Item (Join-Path $GameDir "version.dll") (Join-Path $payloadDir "version.dll") -Force
Copy-Item (Join-Path $GameDir "MelonLoader") (Join-Path $payloadDir "MelonLoader") -Recurse -Force
Copy-Item (Join-Path $GameDir "dotnet") (Join-Path $payloadDir "dotnet") -Recurse -Force
Copy-Item $modDll (Join-Path $payloadDir "Mods\PCFishTool.dll") -Force

# Trim generation caches / runtimes not needed for this IL2CPP build
$trim = @(
    "MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out",
    "MelonLoader\Dependencies\Il2CppAssemblyGenerator\UnityDependencies",
    "MelonLoader\Dependencies\Il2CppAssemblyGenerator\Il2CppInterop",
    "MelonLoader\Logs",
    "MelonLoader\net35",
    "MelonLoader\net472"
)
foreach ($rel in $trim) {
    $path = Join-Path $payloadDir $rel
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

Compress-Archive -Path (Join-Path $payloadDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force
Write-Host ("Payload: {0:N1} MB" -f ((Get-Item $payloadZip).Length / 1MB))

Write-Host "Generating installer source..."
$src = Get-Content -Path $installerSource -Raw -Encoding UTF8
$src = $src.Replace("__VERSION__", $version)
Set-Content -Path $installerGenerated -Value $src -Encoding UTF8

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$setup = Join-Path $outPath ("PCFishTool-Setup-v{0}.exe" -f $version)
Write-Host "Compiling installer..."
$ErrorActionPreference = "Continue"
& $csc /nologo /target:winexe /platform:x64 /out:$setup `
    /win32manifest:$manifest `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.FileSystem.dll `
    /resource:$payloadZip,payload.zip `
    $installerGenerated 2>&1 | ForEach-Object { Write-Host $_ }
$cscCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($cscCode -ne 0) { throw "csc failed" }

Write-Host "Built: $setup"

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
