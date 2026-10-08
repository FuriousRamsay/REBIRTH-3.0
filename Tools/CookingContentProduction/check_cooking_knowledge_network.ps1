$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../RebirthUtils.dll'))
$codec=$assembly.GetType('RebirthSurvivorNetworkCodec')
$snapshot=[RebirthSurvivorOwnerStateSnapshot]::new()
1..600 | ForEach-Object { $snapshot.KnowledgeIds.Add('literature.read.cooking.'+$_) }
$stream=[IO.MemoryStream]::new()
$writer=[IO.BinaryWriter]::new($stream)
$codec.GetMethod('WriteOwnerState').Invoke($null,@($writer.PSObject.BaseObject,$snapshot.PSObject.BaseObject))
$size=$stream.Length
$estimate=$codec.GetMethod('EstimateOwnerState').Invoke($null,@($snapshot.PSObject.BaseObject))
$stream.Position=0
$reader=[IO.BinaryReader]::new($stream)
$result=$codec.GetMethod('ReadOwnerState').Invoke($null,@($reader.PSObject.BaseObject))
if($result.KnowledgeIds.Count -ne 600 -or $stream.Position -ne $size -or $estimate -lt $size){throw 'Knowledge snapshot round-trip or transport size failed'}
for($i=0;$i -lt 600;$i++){if($snapshot.KnowledgeIds[$i] -ne $result.KnowledgeIds[$i]){throw 'Learned title changed during transmission'}}
Write-Output "Network round-trip passed: 600 learned titles, $size bytes, complete stream consumed, transport estimate sufficient."
