<#
    Ordo Cadentia — terminal installer.

    Downloads the latest release from GitHub and installs it, with no windows
    to click. Everything it does is in plain sight below; read it before you
    run it, the same as you should with any script off the internet.

    Install:
        irm https://raw.githubusercontent.com/Fishoes/ordo-cadentia-frame-pacer/main/install.ps1 | iex

    Install in a chosen language, or without shortcuts:
        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/Fishoes/ordo-cadentia-frame-pacer/main/install.ps1))) -Language es
        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/Fishoes/ordo-cadentia-frame-pacer/main/install.ps1))) -NoShortcuts

    Uninstall:
        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/Fishoes/ordo-cadentia-frame-pacer/main/install.ps1))) -Uninstall

    It installs into your own user folder, so it never asks for administrator
    rights. It installs no service, no driver and no scheduled task.
#>

[CmdletBinding()]
param(
    [ValidateSet("en", "pt", "es")] [string] $Language,
    [switch] $NoShortcuts,
    [switch] $Uninstall
)

$ErrorActionPreference = "Stop"

$Repo        = "Fishoes/ordo-cadentia-frame-pacer"
$AppName     = "Ordo Cadentia"
$InstallDir  = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$SettingsDir = Join-Path $env:LOCALAPPDATA "OrdoCadentia"
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\OrdoCadentia"
$DesktopLnk  = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "$AppName.lnk"
$StartMenu   = Join-Path ([Environment]::GetFolderPath("Programs")) $AppName

function Say  ($m) { Write-Host "  $m" }
function Good ($m) { Write-Host "  $m" -ForegroundColor Green }
function Warn ($m) { Write-Host "  $m" -ForegroundColor Yellow }

Write-Host ""
Write-Host "  ORDO CADENTIA" -ForegroundColor Red
Write-Host "  Makes 30 FPS on a PC feel like 30 FPS on a console." -ForegroundColor DarkGray
Write-Host ""

# ---------------------------------------------------------------- running?
$running = Get-Process OrdoCadentia -ErrorAction SilentlyContinue
if ($running) {
    Warn "Ordo Cadentia is open right now."
    Warn "Close it (including the tray icon next to the clock) and run this again."
    return
}

# ================================================================ UNINSTALL
if ($Uninstall) {
    Say "Removing $AppName..."

    # Undo any fullscreen-optimization flags the program left behind. Each line
    # of this file is the full path of an executable it touched.
    $ledger = Join-Path $SettingsDir "fullscreen-flags.txt"
    if (Test-Path $ledger) {
        $layers = "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
        foreach ($exe in (Get-Content $ledger -ErrorAction SilentlyContinue)) {
            $exe = $exe.Trim()
            if (-not $exe) { continue }
            try {
                $v = (Get-ItemProperty $layers -Name $exe -ErrorAction Stop).$exe
                $kept = ($v -split ' ' | Where-Object { $_ -and $_ -ne "DISABLEDXMAXIMIZEDWINDOWEDMODE" }) -join ' '
                if (-not $kept -or $kept -eq "~") { Remove-ItemProperty $layers -Name $exe -ErrorAction SilentlyContinue }
                else { Set-ItemProperty $layers -Name $exe -Value $kept }
            } catch { }
        }
        Remove-Item $ledger -Force -ErrorAction SilentlyContinue
        Say "registry flags undone"
    }

    if (Test-Path $DesktopLnk) { Remove-Item $DesktopLnk -Force -ErrorAction SilentlyContinue }
    if (Test-Path $StartMenu)  { Remove-Item $StartMenu -Recurse -Force -ErrorAction SilentlyContinue }
    Say "shortcuts removed"

    if (Test-Path $UninstallKey) { Remove-Item $UninstallKey -Recurse -Force -ErrorAction SilentlyContinue }
    Say "removed from the Windows program list"

    if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue }
    Say "files removed"

    Write-Host ""
    Good "$AppName removed."
    Say  "Your settings were kept in $SettingsDir — delete that folder to remove them too."
    Write-Host ""
    return
}

# ================================================================ INSTALL
# ---------------------------------------------------------------- release
Say "Looking for the latest release..."
try {
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest" `
                                 -Headers @{ "User-Agent" = "ordo-cadentia-installer" }
} catch {
    Warn "Could not reach the GitHub releases API."
    Warn "Check your connection, or download the installer by hand from:"
    Warn "  https://github.com/$Repo/releases"
    return
}

$tag = $release.tag_name
Say "found $tag"

# The program itself, the uninstaller and all three instruction files.
$wanted = @("OrdoCadentia.exe", "Desinstalar.exe", "LEIA-ME.txt", "README.txt", "LEEME.txt")
$assets = @{}
foreach ($a in $release.assets) { if ($wanted -contains $a.name) { $assets[$a.name] = $a.browser_download_url } }

$missing = $wanted | Where-Object { -not $assets.ContainsKey($_) }
if ($missing -contains "OrdoCadentia.exe" -or $missing -contains "Desinstalar.exe") {
    Warn "Release $tag is missing required files: $($missing -join ', ')"
    Warn "Download the installer by hand instead: https://github.com/$Repo/releases"
    return
}
if ($missing) { Warn "not in this release, skipping: $($missing -join ', ')" }

# ---------------------------------------------------------------- download
# Download to a temporary folder first, so a failed download never leaves a
# half-installed copy behind.
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("ordo-cadentia-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Force -Path $staging | Out-Null

try {
    foreach ($name in $assets.Keys) {
        Say "downloading $name"
        Invoke-WebRequest $assets[$name] -OutFile (Join-Path $staging $name) -UseBasicParsing
    }

    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    foreach ($name in $assets.Keys) {
        Copy-Item (Join-Path $staging $name) (Join-Path $InstallDir $name) -Force
    }
    Good "installed into $InstallDir"
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}

$exe = Join-Path $InstallDir "OrdoCadentia.exe"

# ---------------------------------------------------------------- language
# Written into the settings file the same way the graphical installer does it:
# one line replaced, everything else preserved, so reinstalling never wipes
# the choices of someone who already used the program.
if ($Language) {
    New-Item -ItemType Directory -Force -Path $SettingsDir | Out-Null
    $settingsFile = Join-Path $SettingsDir "settings.ini"
    $lines = if (Test-Path $settingsFile) { @(Get-Content $settingsFile) } else { @() }
    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*Language\s*=') { $lines[$i] = "Language=$Language"; $found = $true; break }
    }
    if (-not $found) { $lines += "Language=$Language" }
    Set-Content $settingsFile $lines -Encoding UTF8
    Say "language set to $Language"
}

# ---------------------------------------------------------------- shortcuts
if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $lnk = $shell.CreateShortcut($DesktopLnk)
        $lnk.TargetPath = $exe; $lnk.WorkingDirectory = $InstallDir
        $lnk.Description = "Frame pacer"; $lnk.IconLocation = "$exe,0"
        $lnk.Save()

        New-Item -ItemType Directory -Force -Path $StartMenu | Out-Null

        $lnk = $shell.CreateShortcut((Join-Path $StartMenu "$AppName.lnk"))
        $lnk.TargetPath = $exe; $lnk.WorkingDirectory = $InstallDir
        $lnk.Description = "Frame pacer"; $lnk.IconLocation = "$exe,0"
        $lnk.Save()

        $uninstExe = Join-Path $InstallDir "Desinstalar.exe"
        $lnk = $shell.CreateShortcut((Join-Path $StartMenu "Uninstall $AppName.lnk"))
        $lnk.TargetPath = $uninstExe; $lnk.WorkingDirectory = $InstallDir
        $lnk.Description = "Removes $AppName"; $lnk.IconLocation = "$uninstExe,0"
        $lnk.Save()

        Say "shortcuts created"
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
}

# ---------------------------------------------------------------- registry
# So it shows up in Windows Settings -> Apps like any normal program.
$size = (Get-ChildItem $InstallDir | Measure-Object -Property Length -Sum).Sum
New-Item -Path $UninstallKey -Force | Out-Null
Set-ItemProperty $UninstallKey DisplayName         $AppName
Set-ItemProperty $UninstallKey DisplayVersion      ($tag -replace '^v', '')
Set-ItemProperty $UninstallKey Publisher           $AppName
Set-ItemProperty $UninstallKey DisplayIcon         $exe
Set-ItemProperty $UninstallKey InstallLocation     $InstallDir
Set-ItemProperty $UninstallKey UninstallString     ('"' + (Join-Path $InstallDir "Desinstalar.exe") + '"')
Set-ItemProperty $UninstallKey QuietUninstallString ('"' + (Join-Path $InstallDir "Desinstalar.exe") + '" /silencioso')
Set-ItemProperty $UninstallKey EstimatedSize       ([int][math]::Max(1, $size / 1024)) -Type DWord
Set-ItemProperty $UninstallKey NoModify            1 -Type DWord
Set-ItemProperty $UninstallKey NoRepair            1 -Type DWord
Say "registered with Windows"

# ---------------------------------------------------------------- done
Write-Host ""
Good "$AppName $tag installed."
Write-Host ""
Say "Next steps:"
Say "  1. Cap your game at 30 FPS and turn VSync on."
Say "  2. Open Ordo Cadentia and pick the game window."
Say "  3. Click Activate."
Write-Host ""
Say "Start it with:  `"$exe`""
Say "Instructions:   $InstallDir\README.txt"
Say "Remove it with: the -Uninstall switch, or Windows Settings -> Apps"
Write-Host ""
Warn "The program asks for administrator rights when it opens. That is expected:"
Warn "games with anti-cheat run elevated, and two of its six adjustments cannot"
Warn "reach them otherwise."
Write-Host ""
