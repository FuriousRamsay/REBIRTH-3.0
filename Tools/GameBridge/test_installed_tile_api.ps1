$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$assemblyPath=Join-Path $root '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'
Add-Type -Path (Join-Path $env:USERPROFILE '.nuget/packages/coverlet.collector/3.1.2/build/netstandard1.0/Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $assemblyPath))
function RequireMethod($typeName,$name,[string[]]$parameters){
 $type=$assembly.MainModule.Types | Where-Object FullName -EQ $typeName
 if(-not $type){throw "Missing installed type $typeName"}
 $match=@($type.Methods | Where-Object {$_.Name -eq $name -and [string]::Join(',',@($_.Parameters | ForEach-Object {$_.ParameterType.FullName})) -eq [string]::Join(',',$parameters)})
 if($match.Count -ne 1){throw "Missing/ambiguous installed signature $typeName.$name($parameters)"}
}
try {
 foreach($name in @('StreamModeRead','StreamModeWrite')){
  $type=$assembly.MainModule.Types | Where-Object FullName -EQ $name
  if(-not $type -or -not $type.IsEnum){throw "Missing global enum $name"}
  $labels=if($name -eq 'StreamModeRead'){@('Persistency','FromServer','FromClient')}else{@('Persistency','ToServer','ToClient')}
  for($i=0;$i -lt 3;$i++){
   $field=$type.Fields | Where-Object Name -EQ $labels[$i]
   if(-not $field.HasConstant -or $field.Constant -ne $i){throw "Changed enum semantics $name.$($labels[$i])"}
  }
 }
 RequireMethod 'TileEntity' 'read' @('PooledBinaryReader','StreamModeRead')
 RequireMethod 'TileEntity' 'write' @('PooledBinaryWriter','StreamModeWrite')
 RequireMethod 'TileEntity' 'InstantiateFromRead' @('PooledBinaryReader','StreamModeRead','TileEntityType','Chunk','System.Int32[]','System.Func`4<System.Int32,System.Int32,System.Int32,BlockValue>')
 RequireMethod 'TEFeatureAbs' 'Read' @('PooledBinaryReader','StreamModeRead')
 RequireMethod 'TEFeatureAbs' 'Write' @('PooledBinaryWriter','StreamModeWrite')
 RequireMethod 'TEFeatureDoor' 'Read' @('PooledBinaryReader','StreamModeRead')
 RequireMethod 'NetPackageTileEntity' 'Setup' @('TileEntity','StreamModeWrite')
 $tile=$assembly.MainModule.Types | Where-Object FullName -EQ 'TileEntity'
 if(($tile.Fields | Where-Object Name -EQ 'Version').Constant -ne 19){throw 'Tile header version changed; custom fork marker audit required'}
 Write-Output ('PASS installed tile API metadata: global read/write enum values, tile and feature serializers, exact farming Harmony targets, packet Setup; native header19. MVID='+$assembly.MainModule.Mvid)
} finally {$assembly.Dispose()}