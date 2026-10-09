param([string]$UserDataDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'MateEngineX.exe'))) {
    throw 'Extract the complete package beside MateEngineX.exe, then run this script again.'
}
$taskManaged = Join-Path $taskRoot 'MateEngineX_Data/Managed'
$taskScripting = Join-Path $taskRoot 'MateEngineX_Data/ScriptingAssemblies.json'
$taskLocalLow = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) '..\LocalLow'))
if ([string]::IsNullOrWhiteSpace($UserDataDirectory)) { $UserDataDirectory = Join-Path $taskLocalLow 'Shinymoon/MateEngineX' }
$taskUserData = [IO.Path]::GetFullPath($UserDataDirectory)
$taskExe = Join-Path $taskRoot 'MateEngineX.exe'
if (Get-CimInstance Win32_Process -Filter "Name = 'MateEngineX.exe'" | Where-Object { $_.ExecutablePath -eq $taskExe }) {
    throw 'Exit MateEngine before migrating the old installation.'
}
$taskMigrationFiles = @(
    [pscustomobject]@{ Path = (Join-Path $taskManaged 'CustomDancePlayer.dll'); Relative = 'MateEngineX_Data/Managed/CustomDancePlayer.dll' },
    [pscustomobject]@{ Path = (Join-Path $taskManaged 'CustomDancePlayer.pdb'); Relative = 'MateEngineX_Data/Managed/CustomDancePlayer.pdb' },
    [pscustomobject]@{ Path = (Join-Path $taskManaged 'Maoxig.RuntimeVmd.dll'); Relative = 'MateEngineX_Data/Managed/Maoxig.RuntimeVmd.dll' },
    [pscustomobject]@{ Path = (Join-Path $taskRoot 'Models/CustomDancePlayer.me'); Relative = 'Models/CustomDancePlayer.me' },
    [pscustomobject]@{ Path = (Join-Path $taskRoot 'MateEngineX_Data/StreamingAssets/Mods/CustomDancePlayer.me'); Relative = 'MateEngineX_Data/StreamingAssets/Mods/CustomDancePlayer.me' },
    [pscustomobject]@{ Path = (Join-Path $taskUserData 'Mods/CustomDancePlayer.me'); Relative = 'UserData/Mods/CustomDancePlayer.me' }
)

$taskJson = $null
$taskHasLegacyRegistration = $false
if (Test-Path -LiteralPath $taskScripting) {
    $taskJson = Get-Content -LiteralPath $taskScripting -Raw | ConvertFrom-Json
    if ($null -eq $taskJson.names -or $null -eq $taskJson.types -or $taskJson.names.Count -ne $taskJson.types.Count) {
        throw 'ScriptingAssemblies.json has inconsistent names/types arrays. No files were changed.'
    }
    foreach ($taskName in $taskJson.names) {
        if ([string]$taskName -ieq 'CustomDancePlayer.dll' -or [string]$taskName -ieq 'Maoxig.RuntimeVmd.dll') {
            $taskHasLegacyRegistration = $true
            break
        }
    }
}

$taskExistingFiles = @($taskMigrationFiles | Where-Object { Test-Path -LiteralPath ([string]$_.Path) })
if ($taskExistingFiles.Count -eq 0 -and -not $taskHasLegacyRegistration) {
    Write-Host 'No CustomDancePlayer 0.1.x installation was found. No migration is needed.'
    exit 0
}

$taskBackup = Join-Path $taskRoot ('_CustomDancePlayer_Backup_' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $taskBackup | Out-Null

$taskMoved = [Collections.Generic.List[object]]::new()
$taskRegistrationChanged = $false
try {
if ($taskHasLegacyRegistration) { Copy-Item -LiteralPath $taskScripting -Destination (Join-Path $taskBackup 'ScriptingAssemblies.json') }
foreach ($taskEntry in $taskExistingFiles) {
    $taskFile = [string]$taskEntry.Path
    $taskDestination = Join-Path $taskBackup ([string]$taskEntry.Relative)
    $taskFile = [IO.Path]::GetFullPath($taskFile)
    $taskDestination = [IO.Path]::GetFullPath($taskDestination)
    if (-not ($taskFile.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $taskFile.StartsWith($taskUserData + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) -or
        -not $taskDestination.StartsWith($taskBackup + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Migration path is outside the expected game, user data, or backup directory.'
    }
    New-Item -ItemType Directory -Path (Split-Path $taskDestination -Parent) -Force | Out-Null
    Move-Item -LiteralPath $taskFile -Destination $taskDestination
    $taskMoved.Add([pscustomobject]@{Source=$taskFile;Backup=$taskDestination})
}

if ($taskHasLegacyRegistration) {
    $taskNames = [Collections.Generic.List[string]]::new()
    $taskTypes = [Collections.Generic.List[object]]::new()
    for ($taskIndex=0; $taskIndex -lt $taskJson.names.Count; $taskIndex++) {
        $taskName = [string]$taskJson.names[$taskIndex]
        if ($taskName -ieq 'CustomDancePlayer.dll' -or $taskName -ieq 'Maoxig.RuntimeVmd.dll') { continue }
        $taskNames.Add($taskName)
        if ($taskIndex -lt $taskJson.types.Count) { $taskTypes.Add($taskJson.types[$taskIndex]) }
    }
    $taskJson.names = $taskNames.ToArray()
    $taskJson.types = $taskTypes.ToArray()
    $taskTemporary = $taskScripting + '.CustomDancePlayer-migration-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($taskTemporary, ($taskJson | ConvertTo-Json -Depth 20 -Compress), [Text.UTF8Encoding]::new($false))
    [IO.File]::Replace($taskTemporary, $taskScripting, (Join-Path $taskBackup 'ScriptingAssemblies.pre-replace.json'))
    $taskRegistrationChanged = $true
}
[pscustomobject]@{Game=$taskRoot;UserData=$taskUserData;Files=$taskMoved.ToArray();RegistrationChanged=$taskHasLegacyRegistration} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskBackup 'migration.json') -Encoding utf8
} catch {
    $taskFailure = $_
    foreach ($taskEntry in $taskMoved) { Move-Item -LiteralPath $taskEntry.Backup -Destination $taskEntry.Source }
    if ($taskRegistrationChanged -and (Test-Path -LiteralPath (Join-Path $taskBackup 'ScriptingAssemblies.json'))) {
        Copy-Item -LiteralPath (Join-Path $taskBackup 'ScriptingAssemblies.json') -Destination $taskScripting -Force
    }
    if ($taskTemporary -and (Test-Path -LiteralPath $taskTemporary)) { Remove-Item -LiteralPath $taskTemporary }
    throw $taskFailure
}

Write-Host "The CustomDancePlayer 0.1.x installation was migrated successfully. Backup: $taskBackup"
Write-Host 'You can now start MateEngine.'
