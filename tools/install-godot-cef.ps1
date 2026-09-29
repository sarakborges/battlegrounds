$ErrorActionPreference = "Stop"

$Version = "v1.16.2"
$AssetName = "godot_cef-$Version.zip"
$ExpectedSha256 = "e8f7a24486e77156862baf5a4433e4b9c9da540fa26205be3fd21e1a027b4117"
$DownloadUrl = "https://github.com/dsh0416/godot-cef/releases/download/$Version/$AssetName"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GameRoot = Join-Path $RepoRoot "src/Battlegrounds.Game"
$AddonsRoot = Join-Path $GameRoot "addons"
$Destination = Join-Path $AddonsRoot "godot_cef"
$NestedDestination = Join-Path $Destination "godot_cef"
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "battlegrounds-godot-cef-$Version"
$ArchivePath = Join-Path $TempRoot $AssetName
$ExtractRoot = Join-Path $TempRoot "extract"
$InstalledExtension = Join-Path $Destination "godot_cef.gdextension"
$InstalledWindowsDll = Join-Path $Destination "bin/x86_64-pc-windows-msvc/gdcef.dll"
$NestedExtension = Join-Path $NestedDestination "godot_cef.gdextension"

function Test-CefInstall {
    return (Test-Path $InstalledExtension -PathType Leaf) -and
           (Test-Path $InstalledWindowsDll -PathType Leaf)
}

function Write-InstalledPaths {
    Write-Host "Godot CEF $Version is installed correctly."
    Write-Host "  Extension: $InstalledExtension"
    Write-Host "  Windows DLL: $InstalledWindowsDll"
}

# Repair the layout produced by the previous installer revision without re-downloading
# the ~1 GB release archive.
if ((-not (Test-Path $InstalledExtension -PathType Leaf)) -and
    (Test-Path $NestedExtension -PathType Leaf)) {
    Write-Host "Repairing nested Godot CEF addon layout..."
    Get-ChildItem -Path $NestedDestination -Force | Move-Item -Destination $Destination -Force
    Remove-Item $NestedDestination -Recurse -Force
}

if (Test-CefInstall) {
    Write-InstalledPaths
    exit 0
}

try {
    if (Test-Path $TempRoot) {
        Remove-Item $TempRoot -Recurse -Force
    }

    New-Item $TempRoot -ItemType Directory -Force | Out-Null
    New-Item $ExtractRoot -ItemType Directory -Force | Out-Null

    Write-Host "Downloading Godot CEF $Version..."
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $ArchivePath

    $ActualSha256 = (Get-FileHash $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($ActualSha256 -ne $ExpectedSha256) {
        throw "Godot CEF archive hash mismatch. Expected $ExpectedSha256, got $ActualSha256."
    }

    Write-Host "Extracting archive..."
    Expand-Archive -Path $ArchivePath -DestinationPath $ExtractRoot -Force

    $GdExtension = Get-ChildItem -Path $ExtractRoot -Filter "godot_cef.gdextension" -File -Recurse | Select-Object -First 1
    if ($null -eq $GdExtension) {
        throw "Could not find godot_cef.gdextension in the downloaded archive."
    }

    $SourceAddon = $GdExtension.Directory.FullName
    New-Item $AddonsRoot -ItemType Directory -Force | Out-Null

    if (Test-Path $Destination) {
        Remove-Item $Destination -Recurse -Force
    }
    New-Item $Destination -ItemType Directory -Force | Out-Null

    # Copy addon contents directly into addons/godot_cef.
    Copy-Item -Path (Join-Path $SourceAddon "*") -Destination $Destination -Recurse -Force

    if (-not (Test-CefInstall)) {
        throw "Godot CEF install failed. Expected $InstalledExtension and $InstalledWindowsDll."
    }

    Write-InstalledPaths
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item $TempRoot -Recurse -Force
    }
}
