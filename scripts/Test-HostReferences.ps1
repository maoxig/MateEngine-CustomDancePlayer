param(
    [Parameter(Mandatory=$true)][string]$Plugin,
    [Parameter(Mandatory=$true)][string]$Managed,
    [string]$Cecil = 'C:\Users\xp\.nuget\packages\mono.cecil\0.11.6\lib\netstandard2.0\Mono.Cecil.dll'
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $Cecil
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory([IO.Path]::GetFullPath($Managed))
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($Plugin), $parameters)
$checked = 0
$failures = [Collections.Generic.List[string]]::new()
foreach ($reference in $assembly.MainModule.GetMemberReferences()) {
    $scope = $reference.DeclaringType.Scope.Name
    if ($scope -notmatch '^(Assembly-CSharp|Kirurobo\.|UnityEngine|Unity\.TextMeshPro|Newtonsoft|NAudio)') { continue }
    $checked++
    try {
        $definition = $reference.Resolve()
        if ($null -eq $definition) { $failures.Add("Unresolved: $($reference.FullName)") }
    } catch { $failures.Add("$($reference.FullName): $($_.Exception.Message)") }
}
$result = [pscustomobject]@{
    Plugin = [IO.Path]::GetFullPath($Plugin)
    Managed = [IO.Path]::GetFullPath($Managed)
    Checked = $checked
    Failures = @($failures.ToArray())
    Note = 'Metadata member resolution only; does not validate Unity scene, AssetBundle, or runtime behavior.'
}
$result | ConvertTo-Json -Depth 5
$assembly.Dispose()
$resolver.Dispose()
if ($failures.Count -gt 0) { exit 1 }
