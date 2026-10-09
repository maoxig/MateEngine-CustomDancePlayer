param(
    [Parameter(Mandatory=$true)][string]$PluginBuildDirectory,
    [Parameter(Mandatory=$true)][string]$CompleteBuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$Version = '0.2.0',
    [string]$RepositoryUrl = 'https://github.com/maoxig/MateEngine-CustomDancePlayer'
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new release output directory.' }
foreach ($taskInput in @(
    @{Build=$PluginBuildDirectory;Manifest='manifest.json';Zip="CustomDancePlayer-$Version.zip"},
    @{Build=$CompleteBuildDirectory;Manifest='complete-manifest.json';Zip="CustomDancePlayer-$Version-With-BepInEx.zip"}
)) {
    $taskManifestPath = Join-Path $taskInput.Build ('package/' + $taskInput.Manifest)
    $taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
    if ($taskManifest.version -ne $Version) { throw 'Release package versions must match.' }
    if (-not (Test-Path -LiteralPath (Join-Path $taskInput.Build $taskInput.Zip))) { throw 'Release archive is missing.' }
    foreach ($taskFile in $taskManifest.files) {
        $taskPath = Join-Path $taskInput.Build ('package/' + $taskFile.path)
        if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskFile.sha256) {
            throw "Package file changed after the manifest was generated: $($taskFile.path)"
        }
    }
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PluginBuildDirectory "CustomDancePlayer-$Version.zip"),
    (Join-Path $CompleteBuildDirectory "CustomDancePlayer-$Version-With-BepInEx.zip") -Destination $OutputDirectory
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskNotes = Get-Content -LiteralPath (Join-Path $taskRepo 'CHANGELOG_zh.md') -Raw
# Release pages resolve relative links differently from repository Markdown.
$taskNotes = $taskNotes.Replace('(docs/', "($RepositoryUrl/blob/v$Version/docs/")
$taskIntro = @"
下载带 With-BepInEx 的整合包，退出游戏后解压到 MateEngineX.exe 同级。从 0.1.x 升级时，再运行 Run-OldVersionMigration.cmd。已有兼容 BepInEx 安装可使用插件包更新。

[使用说明]($RepositoryUrl/blob/v$Version/README_zh.md) · [安装与迁移]($RepositoryUrl/blob/v$Version/docs/user/INSTALL_0.2.md)

"@
$taskNotes = $taskNotes.Replace("## v${Version}：相比 0.1 的变化", "## v${Version}：相比 0.1 的变化`n`n$taskIntro")
Set-Content -LiteralPath (Join-Path $OutputDirectory 'RELEASE_NOTES_zh.md') -Value $taskNotes -Encoding utf8
Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.zip' | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
} | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release files prepared: $OutputDirectory"
