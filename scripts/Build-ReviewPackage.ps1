[CmdletBinding(DefaultParameterSetName="Build")]
param(
 [string]$Version='0.2.0',
 [Parameter(Mandatory=$true,ParameterSetName="Build")][string]$GameManagedDir,
 [Parameter(Mandatory=$true,ParameterSetName="Build")][string]$BepInExCoreDir,
 [Parameter(Mandatory=$true,ParameterSetName="Build")][string]$NativeLibrary,
 [Parameter(Mandatory=$true,ParameterSetName="Build")][string]$ReferencePmx,
 [Parameter(Mandatory=$true,ParameterSetName="Build")][string]$ThirdPartyDirectory,
 [Parameter(Mandatory=$true,ParameterSetName="Verified")][string]$VerifiedPluginDirectory,
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new output directory; existing files will not be replaced.'}
if(Test-Path -LiteralPath (Join-Path $taskOutput 'MateEngineX.exe')){throw 'Output cannot be a game directory.'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskPackage=Join-Path $taskOutput 'package'
$taskPlugin=Join-Path $taskPackage 'BepInEx/plugins/CustomDancePlayer'
if ($PSCmdlet.ParameterSetName -eq 'Verified') {
 $taskRequired=@('CustomDancePlayer.dll','CustomDancePlayer.BepInEx.dll','Maoxig.RuntimeVmd.dll','Assets/customdanceplayer.bundle','Assets/dance-theme.bundle','Locales/en.json','Locales/zh-CN.json','Native/_model.pmx','Native/x86_64/mmd_runtime_ffi.dll','ThirdParty/THIRD_PARTY_NOTICES.md')
 foreach($taskName in $taskRequired){if(-not(Test-Path -LiteralPath (Join-Path $VerifiedPluginDirectory $taskName) -PathType Leaf)){throw "Incomplete verified plugin: $taskName"}}
 $taskAllowedRoots=@('Assets','Locales','Native','ThirdParty')
 foreach($taskEntry in Get-ChildItem -LiteralPath $VerifiedPluginDirectory -Force){
  if($taskEntry.PSIsContainer){if($taskEntry.Name -notin $taskAllowedRoots){throw "Unexpected plugin directory: $($taskEntry.Name)"}}
  elseif($taskEntry.Name -notin @('CustomDancePlayer.dll','CustomDancePlayer.BepInEx.dll','Maoxig.RuntimeVmd.dll')){throw "Unexpected plugin file: $($taskEntry.Name)"}
 }
 New-Item -ItemType Directory -Path (Split-Path $taskPlugin -Parent) -Force | Out-Null
 Copy-Item -LiteralPath $VerifiedPluginDirectory -Destination $taskPlugin -Recurse
} else {
 foreach($taskInput in @($NativeLibrary,$ReferencePmx,(Join-Path $BepInExCoreDir 'BepInEx.dll'),(Join-Path $GameManagedDir 'Assembly-CSharp.dll'))){if(-not(Test-Path -LiteralPath $taskInput)){throw "Missing input: $taskInput"}}
$taskBuild=Join-Path $taskOutput 'build'
dotnet build (Join-Path $taskRepo 'CustomDancePlayer.csproj') -c Release -o $taskBuild "-p:GameManagedDir=$GameManagedDir" --nologo
if($LASTEXITCODE -ne 0){throw 'Player build failed'}
$taskLoader=Join-Path $taskOutput 'loader'
dotnet build (Join-Path $taskRepo 'Loader/CustomDancePlayer.BepInEx.csproj') -c Release -o $taskLoader "-p:GameManagedDir=$GameManagedDir" "-p:BepInExCoreDir=$BepInExCoreDir" --nologo
if($LASTEXITCODE -ne 0){throw 'Loader build failed'}
foreach($taskFolder in @('Assets','Locales','Native/x86_64','ThirdParty')){New-Item -ItemType Directory -Path (Join-Path $taskPlugin $taskFolder) -Force | Out-Null}
Copy-Item -LiteralPath (Join-Path $taskBuild 'CustomDancePlayer.dll'),(Join-Path $taskBuild 'Maoxig.RuntimeVmd.dll'),(Join-Path $taskLoader 'CustomDancePlayer.BepInEx.dll') -Destination $taskPlugin
Copy-Item (Join-Path $taskRepo 'Locales/*.json') -Destination (Join-Path $taskPlugin 'Locales')
Copy-Item -LiteralPath (Join-Path $taskRepo 'resources/runtime/dance-theme.bundle') -Destination (Join-Path $taskPlugin 'Assets')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $taskRepo 'dist/CustomDancePlayer.me'))
try{
 $taskEntry=$taskArchive.Entries | Where-Object {$_.FullName -eq 'customdanceplayer.bundle'} | Select-Object -First 1
 if($null -eq $taskEntry){throw 'Base resource bundle is missing'}
 [IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry,(Join-Path $taskPlugin 'Assets/customdanceplayer.bundle'))
}finally{$taskArchive.Dispose()}
Copy-Item -LiteralPath $NativeLibrary -Destination (Join-Path $taskPlugin 'Native/x86_64/mmd_runtime_ffi.dll')
Copy-Item -LiteralPath $ReferencePmx -Destination (Join-Path $taskPlugin 'Native/_model.pmx')
Copy-Item (Join-Path $ThirdPartyDirectory '*') -Destination (Join-Path $taskPlugin 'ThirdParty') -Recurse
Copy-Item -LiteralPath (Join-Path $taskRepo 'Shared/RuntimeVmd/THIRD_PARTY_NOTICES.md') -Destination (Join-Path $taskPlugin 'ThirdParty')
}
$taskProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskPlugin 'CustomDancePlayer.dll')).ProductVersion
if ($taskProductVersion -ne $Version) { throw "Player version $taskProductVersion does not match package version $Version." }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-OldVersionMigration.ps1'),(Join-Path $PSScriptRoot 'Run-OldVersionMigration.cmd') -Destination $taskPackage
@'
CustomDancePlayer plugin update

1. Exit MateEngine.
2. Merge the BepInEx folder into the directory containing MateEngineX.exe.
3. For upgrades from 0.1.x, run Run-OldVersionMigration.cmd before starting the game.
4. Start MateEngine and press H.

This archive requires the compatible MateEngine BepInEx setup. For a first installation, use the With-BepInEx archive.
Dance folder: MateEngineX_Data/StreamingAssets/CustomDances
Documentation: https://github.com/maoxig/MateEngine-CustomDancePlayer
'@ | Set-Content -LiteralPath (Join-Path $taskPackage 'INSTALL.txt') -Encoding ascii
$taskManifest=Get-ChildItem -LiteralPath $taskPackage -Recurse -File | Sort-Object FullName | ForEach-Object {
 [pscustomobject]@{path=[IO.Path]::GetRelativePath($taskPackage,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$taskCommit=git -C $taskRepo rev-parse HEAD
[pscustomobject]@{version=$Version;files=$taskManifest} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskPackage 'manifest.json') -Encoding utf8
[pscustomobject]@{version=$Version;sourceCommit=$taskCommit;worktree=(git -C $taskRepo status --porcelain);mode=$PSCmdlet.ParameterSetName;verifiedPlugin=$VerifiedPluginDirectory} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'build-provenance.json') -Encoding utf8
$taskArchiveName="CustomDancePlayer-$Version.zip"
Compress-Archive -Path (Join-Path $taskPackage '*') -DestinationPath (Join-Path $taskOutput $taskArchiveName)
Get-FileHash -LiteralPath (Join-Path $taskOutput $taskArchiveName) -Algorithm SHA256
