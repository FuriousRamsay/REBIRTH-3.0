param(
    [string]$ModsPath = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)
# Read-only check for the supported 3.2 b10 Harmony installation. No game launch.
$ErrorActionPreference = 'Stop'
$issues = [Collections.Generic.List[string]]::new()
$harmonyPath = Join-Path $ModsPath '0_TFP_Harmony'
$required = @('0Harmony.dll','TfpHarmony.dll','Mono.Cecil.dll','Mono.Cecil.Mdb.dll',
    'Mono.Cecil.Pdb.dll','Mono.Cecil.Rocks.dll','MonoMod.Backports.dll','MonoMod.Core.dll',
    'MonoMod.Iced.dll','MonoMod.ILHelpers.dll','MonoMod.RuntimeDetour.dll','MonoMod.Utils.dll',
    'System.ValueTuple.dll')
$metadata = Join-Path $harmonyPath 'ModInfo.xml'
if (-not (Test-Path -LiteralPath $metadata -PathType Leaf)) {
    $issues.Add('Missing 0_TFP_Harmony/ModInfo.xml; the game cannot discover the Harmony mod.')
} else {
    try {
        [xml]$info = Get-Content -LiteralPath $metadata -Raw
        if ($info.xml.Name.value -ne 'TFP_Harmony') { $issues.Add('Unexpected Harmony ModInfo identity.') }
    } catch { $issues.Add('Harmony ModInfo.xml could not be read as XML.') }
}
foreach ($name in $required) {
    $path = Join-Path $harmonyPath $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $issues.Add("Missing Harmony dependency: $name")
        continue
    }
    try {
        $assembly = [Reflection.AssemblyName]::GetAssemblyName($path)
        if ($name -eq '0Harmony.dll' -and $assembly.Version -ne [Version]'2.13.0.0') {
            $issues.Add("Unexpected Harmony version $($assembly.Version); this release targets 2.13.0.0.")
        }
    } catch { $issues.Add("Unreadable managed assembly: $name") }
}
[pscustomobject]@{
    passed = $issues.Count -eq 0
    scope = 'Harmony folder completeness and assembly metadata only; not gameplay or full release validation.'
    modsPath = $ModsPath
    issues = @($issues.ToArray())
    remedy = 'Restore the complete 0_TFP_Harmony folder from the matching 7DTD 3.2 b10 installation. Launch modded gameplay with EAC disabled.'
} | ConvertTo-Json -Depth 4
if ($issues.Count) { exit 1 }
