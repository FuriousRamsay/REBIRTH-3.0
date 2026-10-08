#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$foundation=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId')
$end=$foundation.IndexOf('public sealed class RebirthNpcProfile', $start)
if($start-lt0-or$end-lt0){throw 'Actual stable ID extraction failed'}
$codec=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnReplayCodec.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Xml;','')
$request=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcPendingSpawn.cs'))
$persistence=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcPendingSpawnPersistence.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Linq;','').Replace('using System.Xml;','').Replace('using System.Xml.Linq;','')
$fixture=@"
public static class PendingSpawnFixture{
 public static string Run(){int checks=0;RebirthNpcPendingSpawn request,read;var id=RebirthNpcStableId.NewId();
 if(!RebirthNpcPendingSpawn.TryCreate("spawn:one",id,"survivor.ambient","npcRebirthSurvivorManSDCS",1,45,2,90,System.DateTime.UtcNow.Ticks,out request))throw new System.Exception("valid rejected");checks++;
 if(!RebirthNpcPendingSpawn.TryRead(request.Write(),out read)||!System.Xml.Linq.XNode.DeepEquals(request.Write(),read.Write()))throw new System.Exception("roundtrip changed");checks++;
 var image=request.Write();image.SetAttributeValue("x",99);if(request.X!=1)throw new System.Exception("immutable data escaped");checks++;
 foreach(var pair in new[]{new[]{"stable",new string('0',32)},new[]{"key","promote:one"},new[]{"x","NaN"},new[]{"y","4097"},new[]{"yaw","360"},new[]{"created","0"},new[]{"profile"," "}}){image=request.Write();image.SetAttributeValue(pair[0],pair[1]);if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("malformed accepted "+pair[0]);checks++;}
 image=request.Write();image.SetAttributeValue("unknown","x");if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("unknown field accepted");checks++;
 RebirthNpcPendingSpawn attempted;
 if(!request.TryMarkAttempted(out attempted)||!attempted.IsAttempted||request.IsAttempted||attempted.StableId!=request.StableId)throw new System.Exception("phase transition changed intent");checks++;
 if(!RebirthNpcPendingSpawn.TryRead(attempted.Write(),out read)||!read.IsAttempted)throw new System.Exception("attempt phase not preserved");checks++;
 if(attempted.TryMarkAttempted(out read))throw new System.Exception("second attempt admitted");checks++;
 image=attempted.Write();image.SetAttributeValue("phase","prepared");if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("unsupported phase accepted");checks++;
 RebirthNpcPendingSpawn constructed;
 if(!attempted.TryMarkConstructed(42,out constructed)||!constructed.IsConstructed||!constructed.IsAttempted||constructed.NativeEntityId!=42||constructed.StableId!=request.StableId)throw new System.Exception("constructed transition");checks++;
 if(!RebirthNpcPendingSpawn.TryRead(constructed.Write(),out read)||read.NativeEntityId!=42||!read.IsConstructed)throw new System.Exception("constructed roundtrip");checks++;
 if(request.TryMarkConstructed(42,out read)||constructed.TryMarkAttempted(out read)||constructed.TryMarkConstructed(43,out read)||attempted.TryMarkConstructed(0,out read))throw new System.Exception("invalid constructed transition");checks++;
 image=constructed.Write();image.SetAttributeValue("nativeId",0);if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("invalid native ID");checks++;
 System.Collections.Generic.Dictionary<string,RebirthNpcPendingSpawn> records;
 if(!RebirthNpcPendingSpawnPersistence.TryRead(System.Xml.Linq.XElement.Parse("<root/>"),out records)||records.Count!=0)throw new System.Exception("legacy pending rejected");checks++;
 var root=new System.Xml.Linq.XElement("root",new System.Xml.Linq.XElement("pendingSpawns",new System.Xml.Linq.XAttribute("version",1),request.Write()));
 if(!RebirthNpcPendingSpawnPersistence.TryRead(root,out records)||records.Count!=1)throw new System.Exception("pending load failed");checks++;
 var attemptedRoot=new System.Xml.Linq.XElement("root",new System.Xml.Linq.XElement("pendingSpawns",new System.Xml.Linq.XAttribute("version",1),attempted.Write()));
 if(!RebirthNpcPendingSpawnPersistence.TryRead(attemptedRoot,out records)||!records[attempted.ReplayKey].IsAttempted)throw new System.Exception("collection lost attempted phase");checks++;
 var mixedId=RebirthNpcStableId.NewId();RebirthNpcPendingSpawn preparedTwo;
 if(!RebirthNpcPendingSpawn.TryCreate("spawn:two",mixedId,"survivor.ambient","npcRebirthSurvivorManSDCS",2,45,2,90,System.DateTime.UtcNow.Ticks,out preparedTwo))throw new System.Exception("second intent rejected");
 attemptedRoot.Element("pendingSpawns").Add(preparedTwo.Write());
 if(!RebirthNpcPendingSpawnPersistence.TryRead(attemptedRoot,out records)||records.Count!=2||!records[attempted.ReplayKey].IsAttempted||records[preparedTwo.ReplayKey].IsAttempted)throw new System.Exception("mixed phases changed on reload");checks++;
 root.Element("pendingSpawns").Add(request.Write());if(RebirthNpcPendingSpawnPersistence.TryRead(root,out records))throw new System.Exception("duplicate pending accepted");checks++;
 RebirthNpcPendingSpawn publishing;
 if(!constructed.TryMarkPublishing(out publishing)||!publishing.IsPublishing||!publishing.IsConstructed||publishing.NativeEntityId!=42||publishing.StableId!=constructed.StableId)throw new System.Exception("publication transition changed identity");checks++;
 if(!RebirthNpcPendingSpawn.TryRead(publishing.Write(),out read)||!read.IsPublishing||read.NativeEntityId!=42)throw new System.Exception("publication attempt not durable in codec");checks++;
 if(request.TryMarkPublishing(out read)||attempted.TryMarkPublishing(out read)||publishing.TryMarkPublishing(out read)||publishing.TryMarkConstructed(99,out read))throw new System.Exception("publication replay admitted");checks++;
 image=publishing.Write();image.SetAttributeValue("version",3);if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("wrong publication schema");checks++;
 image=publishing.Write();image.SetAttributeValue("nativeId",0);if(RebirthNpcPendingSpawn.TryRead(image,out read))throw new System.Exception("publication missing native identity");checks++;
 return "PASS "+checks+" actual pending-spawn/production stable-ID codec checks; native placement/persistence/creation not exercised";
 }
}
"@
Add-Type -TypeDefinition ($request+"`n"+'using-placeholder'.Replace('using-placeholder','')+$foundation.Substring($start,$end-$start)+$codec+$persistence+$fixture).Replace('using System.Globalization;','using System.Globalization;'+"`nusing System.Collections.Generic;`nusing System.Xml;")
[PendingSpawnFixture]::Run()