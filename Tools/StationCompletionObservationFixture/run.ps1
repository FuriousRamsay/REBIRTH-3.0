$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[Reflection.Assembly]::LoadFrom((Join-Path $root 'Tools/SecondTeam/Medicine/Consumption/observer_out/Mono.Cecil.dll')) | Out-Null
$path = (Resolve-Path (Join-Path $root '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll')).Path
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
$hash = (Get-FileHash $path -Algorithm SHA256).Hash
if ($hash -ne 'FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705') { throw 'Installed assembly changed; requalify exact control flow.' }
$type = $assembly.MainModule.Types | Where-Object Name -eq 'TileEntityWorkstation'
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('Assembly SHA256: ' + $hash)
$lines.Add('MVID: ' + $assembly.MainModule.Mvid)
foreach($name in @('HandleRecipeQueue','AddCraftComplete')) {
$method = $type.Methods | Where-Object Name -eq $name | Select-Object -First 1
$lines.Add('METHOD: ' + $method.FullName)
foreach($instruction in $method.Body.Instructions) {$lines.Add($instruction.ToString())}
if($method.Body.ExceptionHandlers.Count -ne 0) {throw 'Unexpected native exception handlers'}
}
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'INSTALLED_COMPLETION_IL_20261006.txt'),$lines)
$flow = $lines -join "`n"
$checks = @(
'IL_00e4: call System.Int32 ItemStack::AddToItemStackArray',
'IL_00ea: bne.un.s IL_00ed',
'IL_00ec: ret',
'IL_014a: call System.Void TileEntityWorkstation::AddCraftComplete',
'IL_0178: stfld System.Int16 RecipeQueueItem::Multiplier',
'IL_002d: callvirt System.Int32 ItemValue::GetItemId()',
'IL_0046: ldfld System.String CraftCompleteData::ItemScrapped',
'IL_006e: callvirt System.Void ItemStack::set_count(System.Int32)',
'IL_0074: callvirt System.Void TileEntity::setModified()',
'IL_00a6: callvirt System.Void System.Collections.Generic.List`1<CraftCompleteData>::Add(!0)',
'IL_00ac: callvirt System.Void TileEntity::setModified()')
foreach($check in $checks) {if(-not $flow.Contains($check)){throw "Missing exact native IL: $check"}}
Write-Output ('PASS ' + $checks.Count + ' exact installed IL structural checks; no native execution or durable publication qualification.')
} finally {$assembly.Dispose()}