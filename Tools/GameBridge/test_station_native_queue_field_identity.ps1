#Requires -Version 7.0
param()
$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Game running: offline fixture deferred'}
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Crafting/Recycling/RebirthProductiveToolWork.cs'))
$start=$source.IndexOf('    private static bool SameField(')
$end=$source.IndexOf('    internal static IEnumerable<CodeInstruction> Rewrite(',$start)
if($start -lt 0 -or $end -le $start){throw 'Exact field identity/source boundary missing'}
$gate=$source.Substring($start,$end-$start)
$open=$source.IndexOf('{',$end);$depth=1;$at=$open+1
while($depth -gt 0 -and $at -lt $source.Length){if($source[$at] -eq '{'){$depth++};if($source[$at] -eq '}'){$depth--};$at++}
if($depth -ne 0){throw 'Unclosed production rewrite'}
$rewrite=$source.Substring($end,$at-$end)
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'station_native_queue_field_identity_fixture.cs'))
$fixture=$fixture.Replace('// ACTUAL_FIELD_IDENTITY',$gate).Replace('// ACTUAL_REWRITE',$rewrite)
$harmony=Join-Path $root '../0_TFP_Harmony/0Harmony.dll'
Add-Type -Path $harmony
$references=@($harmony)+@(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll'|ForEach-Object FullName)
Add-Type -ReferencedAssemblies $references -TypeDefinition $fixture
[QueueFieldIdentityFixture]::Run()
# Read native metadata only; no game assembly code or static constructor runs.
Add-Type -Path (Join-Path $root '../0_TFP_Harmony/Mono.Cecil.dll')
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
try{
 $type=$game.MainModule.Types|Where-Object Name -eq TileEntityWorkstation
 $method=$type.Methods|Where-Object Name -eq HandleRecipeQueue
 $body=@($method.Body.Instructions)
 $writes=@($body|Where-Object {$_.OpCode.Code -eq 'Stfld' -and $_.Operand.DeclaringType.Name -eq 'RecipeQueueItem' -and $_.Operand.Name -eq 'CraftingTimeLeft'})
 $calls=@($body|Where-Object {$_.OpCode.Code -in @('Call','Callvirt') -and $_.Operand.DeclaringType.Name -eq 'TileEntityWorkstation' -and $_.Operand.Name -eq 'hasRecipeInQueue'})
 $owners=@($body|Where-Object {$_.OpCode.Code -eq 'Ldfld' -and $_.Operand.DeclaringType.Name -eq 'TileEntity' -and $_.Operand.Name -eq 'bUserAccessing'})
 $burning=@($body|Where-Object {$_.OpCode.Code -eq 'Ldfld' -and $_.Operand.DeclaringType.Name -eq 'TileEntityWorkstation' -and $_.Operand.Name -eq 'isBurning'})
 if($writes.Count -ne 3 -or $calls.Count -ne 1 -or $owners.Count -ne 1 -or $burning.Count -ne 1){throw 'Installed native queue shape changed'}
 'PASS installed native metadata: 3 timer writes, 1 queue gate, inherited TileEntity access field, 1 burning gate'
}finally{$game.Dispose()}
