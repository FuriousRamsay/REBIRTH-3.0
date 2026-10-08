#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source='using System;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml;using System.Xml.Linq;'
$foundation=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId');$end=$foundation.IndexOf('public sealed class RebirthNpcProfile',$start)
$source+=$foundation.Substring($start,$end-$start)
$integration=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs'))
$start=$integration.IndexOf('public sealed class RebirthNpcWorldIdentityRecord');$end=$integration.IndexOf('public sealed class RebirthNpcMarkerProjection',$start)
$source+=$integration.Substring($start,$end-$start)
foreach($file in @('RebirthNpcSpawnReplayCodec.cs','RebirthNpcPendingSpawn.cs','RebirthNpcPendingSpawnPersistence.cs','RebirthNpcWorldIdentityLoad.cs')){
 $source+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $taskRoot ('Scripts/NPC/WorldIntegration/'+$file))),'(?m)^using [^\r\n]+;\r?\n','')
}
$fixture=@"
public static class WorldIdentityLoadFixture{
 static int checks;
 static bool Read(string body,bool expected){var doc=new XmlDocument();doc.LoadXml(body);bool actual=RebirthNpcWorldIdentityLoad.TryRead(doc.DocumentElement,out var identities,out var pending,out var replay);if(actual!=expected)throw new Exception("snapshot result mismatch");if(!actual&&(identities!=null||pending!=null||replay!=null))throw new Exception("partial snapshot escaped");checks++;return actual;}
 public static string Run(){
 string identity="<identity stableId='11111111111111111111111111111111' ambientEntityId='42' profileId='survivor.persistent' displayName='Mara' promotedUtcTicks='123'/>";
 string head="<rebirthNpcWorldIntegration version='1'>",tail="</rebirthNpcWorldIntegration>";
 Read(head+identity+tail,true);Read(head+tail,true);
 Read(head+identity+identity+tail,false);
 Read(head+identity+identity.Replace("11111111111111111111111111111111","22222222222222222222222222222222")+tail,false);
 foreach(var pair in new[]{new[]{"stableId='11111111111111111111111111111111'","stableId='bad'"},new[]{"ambientEntityId='42'","ambientEntityId='bad'"},new[]{"promotedUtcTicks='123'","promotedUtcTicks='0'"},new[]{"profileId='survivor.persistent'","profileId=''"}})Read(head+identity.Replace(pair[0],pair[1])+tail,false);
 Read(head+identity+"<replays version='1'><replay key='bad'/></replays>"+tail,false);
 Read(head+identity+"<pendingSpawns version='bad'/>"+tail,false);
 Read(head+identity+"<unknown/>"+tail,false);
 Read(head+identity+"<replays version='1'><replay key='spawn:done'/></replays><pendingSpawns version='1'/>"+tail,true);
 RebirthNpcPendingSpawn prepared,attempted,constructed,publishing;
 if(!RebirthNpcPendingSpawn.TryCreate("spawn:pending",RebirthNpcStableId.NewId(),"specialist.medic","npcRebirthSpecialistmedicManSDCS",1,45,2,90,123,out prepared)||
    !prepared.TryMarkAttempted(out attempted)||!attempted.TryMarkConstructed(77,out constructed)||!constructed.TryMarkPublishing(out publishing))throw new Exception("phase setup");
 foreach(var request in new[]{prepared,attempted,constructed,publishing}){
   string section="<pendingSpawns version='1'>"+request.Write().ToString()+"</pendingSpawns>";
   Read(head+section+tail,true);
   Read(head+section+"<replays version='1'><replay key='spawn:pending'/></replays>"+tail,false);
   Read(head+section+"<replays version='1'><replay key='spawn:other'/></replays>"+tail,true);
 }
 return "PASS "+checks+" actual staged identity/pending/replay load checks with production record/ID/codecs; native cache publication/disk not exercised";
 }
}
"@
Add-Type -TypeDefinition ($source+$fixture)
[WorldIdentityLoadFixture]::Run()