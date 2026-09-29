$ErrorActionPreference = "Stop"

$Version = "v1.16.2"
$AssetName = "godot_cef-$Version.zip"
$ExpectedSha256 = "e8f7a24486e77156862baf5a4433e4b9c9da540fa26205be3fd21e1a027b4117"
$DownloadUrl = "https://github.com/dsh0416/godot-cef/releases/download/$Version/$AssetName"

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GameRoot = Join-Path $RepoRoot "src/Battlegrounds.Game"
$AddonsRoot = Join-Path $GameRoot "addons"
$Destination = Join-Path $AddonsRoot "godot_cef"
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "battlegrounds-godot-cef-$Version"
$ArchivePath = Join-Path $TempRoot $AssetName
$ExtractRoot = Join-Path $TempRoot "extract"
$InstalledExtension = Join-Path $Destination "godot_cef.gdextension"
$InstalledWindowsDll = Join-Path $Destination "bin/x86_64-pc-windows-msvc/gdcef.dll"

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

    # Copy the addon contents, not the addon directory itself. This avoids creating
    # addons/godot_cef/godot_cef/... on PowerShell installations where Copy-Item
    # preserves the source directory name when the destination is absent.
    Copy-Item -Path (Join-Path $SourceAddon "*") -Destination $Destination -Recurse -Force

    if (-not (Test-Path $InstalledExtension -PathType Leaf)) {
        throw "Godot CEF install failed: expected extension file was not created at $InstalledExtension."
    }

    if (-not (Test-Path $InstalledWindowsDll -PathType Leaf)) {
        throw "Godot CEF install failed: expected Windows x64 library was not created at $InstalledWindowsDll."
    }

    Write-Host "Godot CEF $Version installed successfully."
    Write-Host "  Extension: $InstalledExtension"
    Write-Host "  Windows DLL: $InstalledWindowsDll"
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item $TempRoot -Recurse -Force
    }
}
