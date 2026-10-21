$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function Read-Source($path){[IO.File]::ReadAllText((Join-Path $root $path))}
function Names($text){@([regex]::Matches($text,'"([a-z][a-z0-9-]+)"')|ForEach-Object {$_.Groups[1].Value})}
function Array-Text($source,$name){$match=[regex]::Match($source,'\b'+[regex]::Escape($name)+'\s*=\s*\{[^;]+;');if(!$match.Success){throw "Array $name missing"};$match.Value}
function Method-Text($source,$name){
 $start=$source.IndexOf($name+'(');if($start -lt 0){throw "Method $name missing"}
 $open=$source.IndexOf('{',$start);$end=$open+1;$depth=1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 if($depth -ne 0){throw "Incomplete $name"};$source.Substring($start,$end-$start)
}
$preparation=Read-Source 'Scripts/NPC/Persistence/RebirthNpcSaveQuarantinePreparation.cs'
$server=Read-Source 'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineServerAdmission.cs'
$owner=Read-Source 'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineServerOwnerScope.cs'
$bundle=Read-Source 'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineBundle.cs'
$required=Names (Array-Text $preparation 'Required')
if($required.Count -ne 83 -or @($required|Sort-Object -Unique).Count -ne 83){throw 'Required server roster changed'}
$dynamic=@('NativeRoleMembers','PlayerRoleMembers','TransientRoleMembers')|ForEach-Object {Names (Array-Text $server $_)}
$variants=@(@{name='npc';source=$server;method='TryQualifyIncomingServer'},@{name='native_player';source=$owner;method='TryQualifyIncomingOwnerScope'})
$rows=@()
foreach($variant in $variants){
 $method=Method-Text $variant.source $variant.method
 $declared=@([regex]::Matches($method,'Add\(new\[\]\{([^}]+)\}')|ForEach-Object {Names $_.Groups[1].Value})+@($dynamic)
 $missing=@($required|Where-Object {$_ -notin $declared});$extra=@($declared|Where-Object {$_ -notin $required});$duplicates=@($declared|Group-Object|Where-Object Count -gt 1|ForEach-Object Name)
 if($declared.Count -ne 83 -or $missing.Count -or $extra.Count -or $duplicates.Count){throw "Incomplete $($variant.name) roster: declared=$($declared.Count) missing=$missing extra=$extra duplicates=$duplicates"}
 $rows+=[ordered]@{variant=$variant.name;required=83;declared=83;missing=$missing;extra=$extra;duplicates=$duplicates;pass=$true}
}
$client=Names (Array-Text $bundle 'ClientProjectionMembers')
if($client.Count -ne 14 -or @($client|Sort-Object -Unique).Count -ne 14){throw 'Client projection roster changed'}
if($bundle -notmatch 'internal static readonly bool AdmissionEnabled=true;'){throw 'Reviewed source admission gate is not enabled'}
[ordered]@{recorded=[DateTime]::UtcNow.ToString('o');kind='STATIC_SOURCE_ONLY';server=$rows;client_members=14;admission_enabled=$true;native_authenticity_tested=$false}|ConvertTo-Json -Depth 6
