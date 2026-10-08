param([Parameter(Mandatory=$true)][string]$ShadowDll,[Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference='Stop'
# PREPARED ONLY: coordinator authorizes execution after coherent shadow build. No native gameplay methods.
if(Get-Process -Name '7DaysToDie','7DaysToDieServer' -ErrorAction SilentlyContinue){throw 'Game/server must be absent'}
$manifestData=Get-Content -LiteralPath $Manifest -Raw|ConvertFrom-Json
$assemblyPath=[IO.Path]::GetFullPath($ShadowDll)
$manifestRoot=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Manifest))+[IO.Path]::DirectorySeparatorChar
if(!$assemblyPath.StartsWith($manifestRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Only isolated manifested shadow DLL permitted'}
if([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name-eq'RebirthUtils'}){throw 'Use fresh pwsh process; baseline RebirthUtils must not be loaded'}
$managed=$manifestData.NativeManagedRoot
$resolver=[ResolveEventHandler]{param($eventSender,$resolveEvent);$name=([Reflection.AssemblyName]::new($resolveEvent.Name)).Name
 if($name-eq'RebirthUtils'){throw 'No baseline mod fallback allowed'}
 $path=Join-Path $managed ($name+'.dll');if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::LoadFrom($path)}
 return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$unityCore=Join-Path $managed 'UnityEngine.CoreModule.dll'
[Reflection.Assembly]::LoadFrom($unityCore)|Out-Null # metadata-only load; no Unity API invocation
$asm=[Reflection.Assembly]::LoadFrom($assemblyPath)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
function ResolveShadowType($name){$t=$asm.GetType($name,$true);return $t}
function Method($name,$member){return (ResolveShadowType $name).GetMethods($flags)|Where-Object Name -eq $member|Select-Object -First 1}
function Field($object,$name){return ,$object.GetType().GetField($name,$flags).GetValue($object)}
function Image($object){return ,$object.GetType().GetMethod('Write',$flags).Invoke($object,@())}
function Digest($node){$bytes=[Text.Encoding]::UTF8.GetBytes($node.ToString([Xml.Linq.SaveOptions]::DisableFormatting));return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))}
function NewNode($name,$attrs){$n=[Xml.Linq.XElement]::new([Xml.Linq.XName]::Get($name));foreach($key in $attrs.Keys){$n.SetAttributeValue([Xml.Linq.XName]::Get($key),$attrs[$key])};return ,$n}
function PrivateRecord($name,$node){$c=(ResolveShadowType $name).GetConstructor($flags,$null,[Type[]]@([Xml.Linq.XElement]),$null);if(!$c){throw "Missing exact XElement constructor $name"};return $c.Invoke([object[]]@($node))}
function Original($job){
 # Synthetic original XML constructed directly for CLONE/data-binding tests. NOT paid admission/native publication.
 $creation='11111111111111111111111111111111';$definition='A'*64
 $aNode=NewNode 'stationAdmission' @{version=2;phase='publicationAttempted';job=$job;creation=$creation;definition=$definition;x=0;y=0;z=0}
 $aType=ResolveShadowType 'RebirthStationGridAdmission';$ctor=$aType.GetConstructor($flags,$null,[Type[]]@([Xml.Linq.XElement],[string],[string],[string]),$null)
 $a=$ctor.Invoke([object[]]@($aNode,$job,$creation,$definition))
 $iNode=NewNode 'stationTerminalIntent' @{version=1;phase='intent';job=$job;creation=$creation;admission=(Digest $aNode);outcome='completion';actor=42};$i=PrivateRecord 'RebirthStationTerminalIntent' $iNode
 $qNode=NewNode 'stationPublication' @{version=1;phase='nativeQueuedPublished';job=$job;creation=$creation;admission=(Digest $aNode);region='Region/r.0.0.7rg';payload=('B'*64);chunkX=0;chunkZ=0;inputStart=8;inputLength=3;queueStart=11;queueLength=3;queuedOwner=42};$q=PrivateRecord 'RebirthStationPublicationRecord' $qNode
 $attrs=@{version=1;phase='nativeCompletedPublished';job=$job;creation=$creation;admission=(Digest $aNode);intent=(Digest $iNode);queued=(Digest $qNode);definition=$definition;region='Region/r.0.0.7rg';payload=('B'*64);chunkX=0;chunkZ=0}
 $span=[byte[]]@(0,1,2);$spanDigest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($span));$offset=8
 foreach($name in @('input','queue','output','completion')){$attrs[$name+'Start']=$offset;$attrs[$name+'Length']=3;$attrs[$name+'Digest']=$spanDigest;$offset+=3}
 $cNode=NewNode 'stationCompletionPublication' $attrs;$c=PrivateRecord 'RebirthStationCompletionPublication' $cNode
 $pNode=NewNode 'stationCompletionExpectationProjection' @{version=1;job=$job;creation=$creation;admission=(Digest $aNode);intent=(Digest $iNode);queued=(Digest $qNode);definition=$definition;completed=(Digest $cNode)}
 foreach($name in @('input','queue','output','completion')){$child=[Xml.Linq.XElement]::new([Xml.Linq.XName]::Get($name),[Convert]::ToBase64String($span));$pNode.Add($child)}
 $args=[object[]]@($pNode,$a,$i,$q,$c,$null);$ok=(Method 'RebirthStationCompletionExpectationProjection' 'TryReadStored').Invoke($null,$args);if(!$ok){throw 'Actual bound raw parser rejected fixture original schema'}
 return [pscustomobject]@{A=$a;I=$i;Q=$q;C=$c;P=$args[5];Node=$pNode;Job=$job}
}
$script:count=0
function Check($condition,$name){$script:count++;if(!$condition){throw $name}}
try{
 $state=[Activator]::CreateInstance((ResolveShadowType 'RebirthWorldProgressionState'))
 $clone=(Method 'RebirthWorldProgressionState' 'Clone').Invoke($state,@())
 Check ((Field $clone 'StationCompletionExpectationProjections').Count-eq0) 'actual whole Clone legacy empty field'
 Check (![object]::ReferenceEquals((Field $state 'StationCompletionExpectationProjections'),(Field $clone 'StationCompletionExpectationProjections'))) 'actual whole Clone dictionary independence'
 $records=@((Original '22222222222222222222222222222222'),(Original '33333333333333333333333333333333'))
 $mapping=@{StationPreparations='A';StationTerminalIntents='I';StationPublications='Q';StationCompletionPublications='C';StationCompletionExpectationProjections='P'}
 foreach($record in $records){foreach($key in $mapping.Keys){(Field $state $key).Add($record.Job,$record.($mapping[$key]))}}
 $state.HealthPotential=123;$state.KnowledgeIds.Add('clone-sibling')|Out-Null
 $clone=(Method 'RebirthWorldProgressionState' 'Clone').Invoke($state,@())
 Check ($clone.HealthPotential-eq123-and$clone.KnowledgeIds.Contains('clone-sibling')) 'actual whole Clone preserves sibling scalar/set'
 foreach($key in $mapping.Keys){$original=Field $state $key;$copied=Field $clone $key;Check ($copied.Count-eq2-and![object]::ReferenceEquals($original,$copied)) ('actual collection independent '+$key)
 foreach($record in $records){Check (![object]::ReferenceEquals($original[$record.Job],$copied[$record.Job])) ('actual immutable DTO Clone distinct '+$key);Check ([Xml.Linq.XNode]::DeepEquals((Image $original[$record.Job]),(Image $copied[$record.Job]))) ('actual DTO evidence retained '+$key)}}
 $projection=(Field $clone 'StationCompletionExpectationProjections')[$records[0].Job];$before=Image $projection;$detached=Image $projection;$detached.Element('input').Value='AAAA';Check ([Xml.Linq.XNode]::DeepEquals($before,(Image $projection))) 'actual projection Write detached immutable evidence'
 (Field $clone 'StationCompletionExpectationProjections').Remove($records[0].Job)|Out-Null;Check ((Field $state 'StationCompletionExpectationProjections').Count-eq2) 'clone collection removal original unchanged'
 $write=Method 'RebirthStationCompletionExpectationProjection' 'WriteAllStored';$args=[object[]]@((Field $state 'StationCompletionExpectationProjections'),(Field $state 'StationPreparations'),(Field $state 'StationTerminalIntents'),(Field $state 'StationPublications'),(Field $state 'StationCompletionPublications'))
 $section=$write.Invoke($null,$args);Check (@($section.Elements()).Count-eq2) 'actual raw collection multiple originals preserved without catalogue access'
 Check ($section.Elements()|Select-Object -First 1|ForEach-Object {$_.Attribute('job').Value-eq$records[0].Job}) 'actual raw writer sorted job'
 $read=Method 'RebirthStationCompletionExpectationProjection' 'ReadAllStored'
 $wrapper=[Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('progression'));$wrapper.Add([Xml.Linq.XElement]::new($section));$readArgs=[object[]]@($wrapper,(Field $state 'StationPreparations'),(Field $state 'StationTerminalIntents'),(Field $state 'StationPublications'),(Field $state 'StationCompletionPublications'),$null)
 Check ($read.Invoke($null,$readArgs)-and$readArgs[5].Count-eq2) 'actual raw collection read no native catalogue argument'
 $duplicate=[Xml.Linq.XElement]::new($wrapper);$duplicate.Element('stationCompletionExpectationProjections').Add([Xml.Linq.XElement]::new($records[0].Node));$readArgs[0]=$duplicate;$readArgs[5]=$null
 Check (!$read.Invoke($null,$readArgs)-and$null-eq$readArgs[5]) 'actual duplicate collection atomic outnull'
 $bad=[Xml.Linq.XElement]::new($wrapper);$bad.Element('stationCompletionExpectationProjections').Add([Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('bad')));$readArgs[0]=$bad;$readArgs[5]=$null
 Check (!$read.Invoke($null,$readArgs)-and$null-eq$readArgs[5]) 'actual malformed second record atomic outnull'
 $readArgs[0]=[Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('progression'));$readArgs[5]=$null
 Check ($read.Invoke($null,$readArgs)-and$readArgs[5].Count-eq0) 'actual raw legacy section absence independent catalogue'
 $tooMany=[Activator]::CreateInstance((Field $state 'StationCompletionExpectationProjections').GetType());for($i=0;$i-lt65;$i++){$tooMany.Add(('count'+$i),$records[0].P)};$args[0]=$tooMany;$rejected=$false;try{$write.Invoke($null,$args)|Out-Null}catch{$cause=$_.Exception;while($cause.InnerException){$cause=$cause.InnerException};$rejected=$cause-is[IO.InvalidDataException]}
 Check $rejected 'actual raw writer65 record section cap'
 $serialize=Method 'RebirthWorldCharacterRepository' 'SerializeProgression';$deserialize=Method 'RebirthWorldCharacterRepository' 'TryDeserializeProgression'
 $legacy=[Activator]::CreateInstance((ResolveShadowType 'RebirthWorldProgressionState'));$legacy.HealthPotential=145;$legacy.KnowledgeIds.Add('synthetic-sibling')|Out-Null;$legacy.AccomplishmentIds.Add('synthetic-achievement')|Out-Null
 # Complete synthetic Survivor definitions are pure managed DTOs; no native item/recipe registry is initialized.
 function TypedList($name){return ,[Activator]::CreateInstance(([Collections.Generic.List[object]].GetGenericTypeDefinition()).MakeGenericType([Type[]]@((ResolveShadowType $name))))}
 $attributeDefs=TypedList 'RebirthAttributeDefinition';foreach($id in @('strength','dexterity','constitution','intelligence','charisma')){
 $attributeDefs.Add([Activator]::CreateInstance((ResolveShadowType 'RebirthAttributeDefinition'),[object[]]@($id,$id,[single]10,[single]50,[single]0,[single]100)))
 $r=[Activator]::CreateInstance((ResolveShadowType 'RebirthAttributeRuntimeState'));$r.AttributeId=$id;$r.Current=10;$r.Potential=50;$legacy.Attributes.Add($id,$r)}
 $skillDefs=TypedList 'RebirthSkillDefinition';$ids=(Method 'RebirthSurvivorSkillMigrationPolicy' 'GetCurrentSkillIds').Invoke($null,@());foreach($id in $ids){
 $skillDefs.Add([Activator]::CreateInstance((ResolveShadowType 'RebirthSkillDefinition'),[object[]]@($id,$id,'fixture','fixture','fixture',[single]-50,[single]100,'strength',$false)))
 $r=[Activator]::CreateInstance((ResolveShadowType 'RebirthSkillRuntimeState'));$r.SkillId=$id;$r.Value=0;$r.Progress=0;$legacy.Skills.Add($id,$r)
 $k=[Activator]::CreateInstance((ResolveShadowType 'RebirthSkillKnowledgeRuntimeState'));$k.SkillId=$id;$k.Value=0;$legacy.SkillKnowledge.Add($id,$k)}
 $knowledgeDefs=TypedList 'RebirthKnowledgeDefinition';$tiers=[Collections.Generic.Dictionary[string,int]]::new()
 $progressionDefs=[Activator]::CreateInstance((ResolveShadowType 'RebirthProgressionDefinition'),[object[]]@(0,0,0,[single]-50,[single]100,[single]0,[single]100,[single]100,[single]0,[single]200,$attributeDefs,$tiers,$skillDefs,$knowledgeDefs))
 $bundle=[Activator]::CreateInstance((ResolveShadowType 'RebirthSurvivorDefinitionBundle'),[object[]]@('standalone-fixture',$progressionDefs,$null,$null,$null,$null,$null,$null))
 (Method 'RebirthSurvivorDefinitionRegistry' 'Install').Invoke($null,[object[]]@($bundle))|Out-Null
 $ownerKey='A'*64;$xml=$serialize.Invoke($null,[object[]]@($legacy,$ownerKey));Check ($null-eq$xml.Element('stationCompletionExpectationProjections')) 'WHOLE serializer legacy projection absent canonical omit'
 $readArgs=[object[]]@($xml,$ownerKey,$null,$null);Check ($deserialize.Invoke($null,$readArgs)) ('WHOLE reader legacy succeeds '+$readArgs[3]);$loaded=$readArgs[2]
 Check ($loaded.HealthPotential-eq145-and$loaded.KnowledgeIds.Contains('synthetic-sibling')-and$loaded.AccomplishmentIds.Contains('synthetic-achievement')) 'WHOLE reader preserves unrelated scalar and set siblings'
 Check ((Field $loaded 'StationCompletionExpectationProjections').Count-eq0) 'WHOLE reader legacy projection empty'
 $duplicate=[Xml.Linq.XElement]::new($xml);foreach($n in 1..2){$empty=NewNode 'stationCompletionExpectationProjections' @{version=1};$duplicate.Add($empty)};$readArgs=[object[]]@($duplicate,$ownerKey,$null,$null)
 Check (!$deserialize.Invoke($null,$readArgs)-and(Field $readArgs[2] 'StationCompletionExpectationProjections').Count-eq0) 'WHOLE reader duplicate section refuses no projection insertion'
 $bad=[Xml.Linq.XElement]::new($xml);$sectionBad=NewNode 'stationCompletionExpectationProjections' @{version=1};$sectionBad.Add([Xml.Linq.XElement]::new([Xml.Linq.XName]::Get('bad')));$bad.Add($sectionBad);$readArgs=[object[]]@($bad,$ownerKey,$null,$null)
 Check (!$deserialize.Invoke($null,$readArgs)-and(Field $readArgs[2] 'StationCompletionExpectationProjections').Count-eq0) 'WHOLE reader malformed record refuses no projection insertion'
 Write-Output ('PASS '+$script:count+' actual full Clone +WHOLE legacy progression serializer/reader; nonempty parent-admission load UNTESTED/no nativeauth')
}finally{[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)}
