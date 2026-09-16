# build.ps1 — builds the whole of Ordo Cadentia from source.
#
#   .\build.ps1           builds everything into dist\
#   .\build.ps1 -Tests    builds, runs the full test suite, then packages
#
# No Visual Studio and no package restore required: this uses the Roslyn
# compiler that ships with the .NET SDK, linked against the .NET Framework 4.x
# libraries that already exist on every Windows 10/11 machine. That is why the
# binaries are small and run anywhere without a runtime download.

param([switch]$Tests)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $fw)) { throw ".NET Framework 4.x not found at $fw" }

$sdk = Get-ChildItem "C:\Program Files\dotnet\sdk" -Directory -ErrorAction SilentlyContinue |
       Where-Object { Test-Path (Join-Path $_.FullName "Roslyn\bincore\csc.dll") } |
       Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdk) { throw ".NET SDK with Roslyn not found. Install .NET SDK 6 or newer." }
$csc = Join-Path $sdk.FullName "Roslyn\bincore\csc.dll"

$refs = @("mscorlib.dll","System.dll","System.Core.dll","System.Drawing.dll","System.Windows.Forms.dll") |
        ForEach-Object { "-r:$fw\$_" }

$build = Join-Path $root "build"
$dist  = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $build, $dist | Out-Null

function Compile {
    param([string]$Out, [string]$Kind, [string[]]$Sources,
          [string]$Manifest, [string]$Icon, [string[]]$Resources)

    # -deterministic: the same source always produces a byte-identical binary.
    # Without it every build embeds a fresh identifier, and "is the installed
    # copy the current version?" stops being answerable by comparing hashes.
    $cmd = @("exec", $csc, "-nologo", "-noconfig", "-nostdlib+", "-langversion:latest",
             "-platform:x64", "-optimize+", "-deterministic", "-target:$Kind", "-out:$Out") + $refs
    if ($Manifest) { $cmd += "-win32manifest:$Manifest" }
    if ($Icon)     { $cmd += "-win32icon:$Icon" }
    foreach ($r in $Resources) { $cmd += "-resource:$r" }
    $cmd += $Sources

    $output = & dotnet @cmd 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "failed to compile $Out"
    }
    $errors = $output | Where-Object { $_ -match "error CS" }
    if ($errors) { $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }; throw "errors in $Out" }

    $kb = [math]::Round((Get-Item $Out).Length / 1KB)
    Write-Host ("  OK  {0,-34} {1,5} KB" -f (Split-Path $Out -Leaf), $kb) -ForegroundColor Green
}

$appSources = Get-ChildItem "src\OrdoCadentia\*.cs" | ForEach-Object { $_.FullName }

$themeSources = @("src\OrdoCadentia\Theme.cs", "src\OrdoCadentia\Controls.cs",
                  "src\OrdoCadentia\Strings.cs") | ForEach-Object { Join-Path $root $_ }

$engineSources = @("src\OrdoCadentia\Native.cs", "src\OrdoCadentia\Platform.cs",
                   "src\OrdoCadentia\Engine.cs", "src\OrdoCadentia\Theme.cs",
                   "src\OrdoCadentia\Controls.cs", "src\OrdoCadentia\Strings.cs") |
                 ForEach-Object { Join-Path $root $_ }

Write-Host ""
Write-Host "ORDO CADENTIA — building" -ForegroundColor Yellow
Write-Host ""

# ---------------------------------------------------------------- 1. icon
Write-Host "icon" -ForegroundColor DarkYellow
Compile -Out "$build\MakeIcon.exe" -Kind exe -Sources @("$root\src\Tools\MakeIcon.cs")
& "$build\MakeIcon.exe" "$build\OrdoCadentia.ico" | Out-Null
$icon = "$build\OrdoCadentia.ico"

# ---------------------------------------------------------------- 2. program
Write-Host ""
Write-Host "program" -ForegroundColor DarkYellow
Compile -Out "$build\OrdoCadentia.exe" -Kind winexe -Sources $appSources `
        -Manifest "$root\src\OrdoCadentia\app.manifest" -Icon $icon

# ---------------------------------------------------------------- 3. uninstaller
Compile -Out "$build\Desinstalar.exe" -Kind winexe `
        -Sources ($engineSources + @("$root\src\Uninstaller\Uninstaller.cs")) `
        -Manifest "$root\src\Uninstaller\app.manifest" -Icon $icon

# ---------------------------------------------------------------- 4. tests
if ($Tests) {
    Write-Host ""
    Write-Host "tests" -ForegroundColor DarkYellow
    Compile -Out "$build\LanguageTest.exe" -Kind exe `
            -Sources @("$root\src\OrdoCadentia\Strings.cs", "$root\src\Tools\LanguageTest.cs")
    Compile -Out "$build\SelfTest.exe" -Kind exe `
            -Sources @("$root\src\OrdoCadentia\Native.cs", "$root\src\OrdoCadentia\Platform.cs",
                       "$root\src\OrdoCadentia\Strings.cs", "$root\src\Tools\SelfTest.cs")
    Compile -Out "$build\DummyTarget.exe" -Kind winexe -Sources @("$root\src\Tools\DummyTarget.cs")
    Compile -Out "$build\EndToEndTest.exe" -Kind exe `
            -Sources @("$root\src\OrdoCadentia\Native.cs", "$root\src\OrdoCadentia\Platform.cs",
                       "$root\src\OrdoCadentia\Engine.cs", "$root\src\OrdoCadentia\Theme.cs",
                       "$root\src\OrdoCadentia\Strings.cs", "$root\src\Tools\EndToEndTest.cs")
    Write-Host ""
    & "$build\LanguageTest.exe"; if ($LASTEXITCODE -ne 0) { throw "language test failed" }
    & "$build\SelfTest.exe";     if ($LASTEXITCODE -ne 0) { throw "self test failed" }
    & "$build\EndToEndTest.exe"; if ($LASTEXITCODE -ne 0) { throw "end-to-end test failed" }
}

# ---------------------------------------------------------------- 5. installer
Write-Host ""
Write-Host "installer" -ForegroundColor DarkYellow
$docs = @("LEIA-ME.txt", "README.txt", "LEEME.txt")
foreach ($d in $docs) { Copy-Item "$root\resources\$d" "$build\$d" -Force }

Compile -Out "$dist\Instalar-OrdoCadentia.exe" -Kind winexe `
        -Sources ($themeSources + @("$root\src\Installer\Installer.cs")) `
        -Manifest "$root\src\Installer\app.manifest" -Icon $icon `
        -Resources @("$build\OrdoCadentia.exe,OrdoCadentia.exe",
                     "$build\Desinstalar.exe,Desinstalar.exe",
                     "$build\LEIA-ME.txt,LEIA-ME.txt",
                     "$build\README.txt,README.txt",
                     "$build\LEEME.txt,LEEME.txt")

# ---------------------------------------------------------------- 6. loose files
Copy-Item "$build\OrdoCadentia.exe" "$dist\OrdoCadentia.exe" -Force
Copy-Item "$build\Desinstalar.exe"  "$dist\Desinstalar.exe"  -Force
foreach ($d in $docs) { Copy-Item "$build\$d" "$dist\$d" -Force }

Write-Host ""
Write-Host "done — dist\" -ForegroundColor Yellow
Get-ChildItem $dist | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-34} {1,6} KB" -f $_.Name, [math]::Round($_.Length / 1KB))
}
Write-Host ""
