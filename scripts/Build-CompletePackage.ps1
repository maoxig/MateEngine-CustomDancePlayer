param(
    [Parameter(Mandatory=$true)][string]$PluginPackageDirectory,
    [Parameter(Mandatory=$true)][string]$BepInExArchive,
    [Parameter(Mandatory=$true)][string]$UnstrippedSourceDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$Version = '0.2.0'
)

$ErrorActionPreference = 'Stop'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $taskOutput) { throw 'Use a new output directory; existing files will not be replaced.' }
foreach ($taskInput in @($PluginPackageDirectory, $BepInExArchive, $UnstrippedSourceDirectory)) {
    if (-not (Test-Path -LiteralPath $taskInput)) { throw "Missing input: $taskInput" }
}
foreach ($taskName in @('mscorlib.dll','System.dll','System.Core.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $UnstrippedSourceDirectory $taskName))) { throw "Missing unstripped runtime file: $taskName" }
}
$taskPluginManifest = Get-Content -LiteralPath (Join-Path $PluginPackageDirectory 'manifest.json') -Raw | ConvertFrom-Json
if ($taskPluginManifest.version -ne $Version) { throw 'Plugin and complete package versions must match.' }

New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskRoot = Join-Path $taskOutput 'package'
Expand-Archive -LiteralPath $BepInExArchive -DestinationPath $taskRoot
# The upstream archive includes a release changelog; keep installation files only.
$taskUpstreamChangelog = Join-Path $taskRoot 'changelog.txt'
if (Test-Path -LiteralPath $taskUpstreamChangelog -PathType Leaf) { Remove-Item -LiteralPath $taskUpstreamChangelog }
$taskBepInExRoot = Join-Path $taskRoot 'BepInEx'
New-Item -ItemType Directory -Path $taskBepInExRoot -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PluginPackageDirectory 'BepInEx') -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $taskBepInExRoot -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'BepInEx/unstripped'),(Join-Path $taskRoot 'BepInEx/config') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $UnstrippedSourceDirectory 'mscorlib.dll'),(Join-Path $UnstrippedSourceDirectory 'System.dll'),(Join-Path $UnstrippedSourceDirectory 'System.Core.dll') -Destination (Join-Path $taskRoot 'BepInEx/unstripped')

$taskDoorstop = Join-Path $taskRoot 'doorstop_config.ini'
$taskDoorstopText = Get-Content -LiteralPath $taskDoorstop -Raw
$taskDoorstopText = [regex]::Replace($taskDoorstopText, '(?m)^dll_search_path_override\s*=.*$', 'dll_search_path_override = BepInEx\unstripped')
Set-Content -LiteralPath $taskDoorstop -Value $taskDoorstopText -Encoding utf8
@'
[Logging]
UnityLogListening = false
LogConsoleToUnityLog = false

[Logging.Console]
Enabled = false
'@ | Set-Content -LiteralPath (Join-Path $taskRoot 'BepInEx/config/BepInEx.cfg') -Encoding utf8

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-OldVersionMigration.ps1'),(Join-Path $PSScriptRoot 'Run-OldVersionMigration.cmd') -Destination $taskRoot
# Retain the required redistribution notice in one file rather than a Licenses folder.
$taskNotice = "BepInEx license`r`n`r`n" + (Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'resources/licenses/BepInEx-LICENSE.txt') -Raw)
$taskNotice += "`r`nCustomDancePlayer runtime dependency notices: BepInEx/plugins/CustomDancePlayer/ThirdParty`r`n"
Set-Content -LiteralPath (Join-Path $taskRoot 'NOTICE.txt') -Value $taskNotice -Encoding ascii
@"
CustomDancePlayer $Version + BepInEx 5.4.23.5

1. Exit MateEngine.
2. Open the game directory that contains MateEngineX.exe.
3. Extract every file from this archive into that directory. winhttp.dll must be beside MateEngineX.exe.
4. For a new installation, start the game. When upgrading from CustomDancePlayer 0.1.x, run Run-OldVersionMigration.cmd first.
5. The runtime log is BepInEx\LogOutput.log.

Press H to open the player.
Dance folder: MateEngineX_Data/StreamingAssets/CustomDances
Documentation: https://github.com/maoxig/MateEngine-CustomDancePlayer
"@ | Set-Content -LiteralPath (Join-Path $taskRoot 'INSTALL.txt') -Encoding ascii

$taskManifest = Get-ChildItem -LiteralPath $taskRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($taskRoot,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
[pscustomobject]@{version=$Version;bepInEx='5.4.23.5';files=$taskManifest} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot 'complete-manifest.json') -Encoding utf8
$taskZip = Join-Path $taskOutput "CustomDancePlayer-$Version-With-BepInEx.zip"
Compress-Archive -Path (Join-Path $taskRoot '*') -DestinationPath $taskZip
Get-FileHash -LiteralPath $taskZip -Algorithm SHA256
