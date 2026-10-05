# Builds the release archives in dist/:
#   DMDItemEditor-<version>.zip             BepInEx + config + plugin, extract into the game folder
#   DMDItemEditor-<version>-plugin-only.zip plugin only, for players who already have BepInEx 5
param(
    [string]$GameDir = "G:\Steam\steamapps\common\Death Must Die",
    [string]$BepInExVersion = "5.4.23.5"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src\DMDItemEditor\DMDItemEditor.csproj"
$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dist = Join-Path $root "dist"
$work = Join-Path $dist "work"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null

# Deploy=false: packaging must work while the game (and its locked plugin DLL) is running.
dotnet build $proj -c Release -nologo -p:GameDir="$GameDir" -p:Deploy=false | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
$dll = Join-Path $root "src\DMDItemEditor\bin\Release\netstandard2.1\DMDItemEditor.dll"

# BepInEx 5 x64 from the official release (LGPL-2.1, redistributable).
$bepZip = Join-Path $dist "BepInEx_win_x64_$BepInExVersion.zip"
if (-not (Test-Path $bepZip)) {
    Invoke-WebRequest "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip" -OutFile $bepZip
}
$full = Join-Path $work "full"
Expand-Archive $bepZip $full -Force

# Death Must Die destroys BepInEx's manager object unless it is hidden; missing keys keep their defaults.
New-Item -ItemType Directory -Force (Join-Path $full "BepInEx\config") | Out-Null
Set-Content (Join-Path $full "BepInEx\config\BepInEx.cfg") "[Chainloader]`r`n`r`nHideManagerGameObject = true`r`n" -Encoding UTF8

$pluginDir = Join-Path $full "BepInEx\plugins\DMDItemEditor"
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item $dll $pluginDir
Copy-Item (Join-Path $root "LISEZMOI.txt") $full

$fullZip = Join-Path $dist "DMDItemEditor-$version.zip"
Compress-Archive -Path (Join-Path $full "*") -DestinationPath $fullZip -Force

$only = Join-Path $work "plugin-only"
New-Item -ItemType Directory -Force (Join-Path $only "BepInEx\plugins\DMDItemEditor") | Out-Null
Copy-Item $dll (Join-Path $only "BepInEx\plugins\DMDItemEditor")
$onlyZip = Join-Path $dist "DMDItemEditor-$version-plugin-only.zip"
Compress-Archive -Path (Join-Path $only "*") -DestinationPath $onlyZip -Force

Remove-Item $work -Recurse -Force
Get-Item $fullZip, $onlyZip | Select-Object Name, Length
