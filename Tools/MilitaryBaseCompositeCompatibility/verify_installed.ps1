param([Parameter(Mandatory=$true)][string]$ProductionRoot,[string]$GameRoot)
$ErrorActionPreference='Stop'
if(!$GameRoot){$GameRoot=Split-Path (Split-Path $ProductionRoot -Parent) -Parent}
Add-Type -Path (Join-Path $GameRoot 'Mods/0_TFP_Harmony/Mono.Cecil.dll')
$m=Get-Content (Join-Path $PSScriptRoot 'source-manifest.json') -Raw | ConvertFrom-Json
foreach($e in $m.sources){if((Get-FileHash -LiteralPath (Join-Path $ProductionRoot $e.path)).Hash -ne $e.proposed){throw 'Production source differs from reviewed payload'}}
$dll=Join-Path $ProductionRoot 'RebirthUtils.dll';$before=(Get-FileHash -LiteralPath $dll).Hash
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
try {
 if($a.Name.Name -ne 'RebirthUtils'){throw 'Wrong installed assembly identity'}
 $t=$a.MainModule.Types | Where-Object FullName -eq 'BlockRebirthLegacyMilitaryPalletBase'
 if(!$t -or $t.BaseType.FullName -ne 'BlockCompositeTileEntity'){throw 'Installed native compatibility class absent/wrong'}
 foreach($name in @('OnBlockAdded','OnBlockEntityTransformAfterActivated')){
  $method=$t.Methods | Where-Object Name -eq $name
  if(!$method -or !$method.IsVirtual -or !($method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'isinst' -and $_.Operand.FullName -eq 'TileEntityComposite'})){throw "Installed $name lacks existing-composite guard"}
  if(!($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'BlockCompositeTileEntity' -and $_.Operand.Name -eq $name})){throw "Installed $name lacks native legacy callback"}
 }
 $init=$t.Methods | Where-Object Name -eq 'Init'
 if(!($init.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'Deny'})){throw 'Installed pickup denial absent'}
 [xml]$config=Get-Content (Join-Path $ProductionRoot 'Config/_LootConversions/blocks.xml') -Raw
 $node=$config.configs.append | Where-Object {$_.xpath -eq "/blocks/block[@name='palletCartonMilitaryBase']"}
 if(!($node.property | Where-Object {$_.name -eq 'Class' -and $_.value -eq 'RebirthLegacyMilitaryPalletBase, RebirthUtils'})){throw 'Installed compatibility XML class absent'}
 if($before -ne (Get-FileHash -LiteralPath $dll).Hash){throw 'Installed DLL changed during inspection'}
 [pscustomobject]@{Result='PASS source/XML/compiled-class guards';InstalledDllSha256=$before;UserGameplay='NOT_RUN; still required'} | ConvertTo-Json
} finally {$a.Dispose()}
