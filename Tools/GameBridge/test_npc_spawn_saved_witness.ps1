#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source='using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml;using System.Xml.Linq;'
$foundation=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$start=$foundation.IndexOf('public readonly struct RebirthNpcStableId');$end=$foundation.IndexOf('public sealed class RebirthNpcProfile',$start)
$source+=$foundation.Substring($start,$end-$start)
$integration=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs'))
$start=$integration.IndexOf('public sealed class RebirthNpcWorldIdentityRecord');$end=$integration.IndexOf('public sealed class RebirthNpcMarkerProjection',$start)
$source+=$integration.Substring($start,$end-$start)
foreach($file in @('RebirthNpcSpawnReplayCodec.cs','RebirthNpcPendingSpawn.cs','RebirthNpcPendingSpawnPersistence.cs','RebirthNpcWorldIdentityLoad.cs')){
 $source+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $taskRoot ('Scripts/NPC/WorldIntegration/'+$file))),'(?m)^using [^\r\n]+;\r?\n','')
}
$start=$integration.IndexOf('    internal static bool HasSavedPendingSpawn(')
$end=$integration.IndexOf('    public static bool TryComposeSpawn(', $start)
if($start-lt0-or$end-lt0){throw 'Witness extraction failed'}
$source+='public static class GameIO{public static string Directory;public static string GetSaveGameDir()=>Directory;}'
$source+='public static class WitnessHost{private static readonly object Sync=new object();private const string FileName="RebirthNpcWorldIntegration.xml";public static bool Server=true;private static bool IsServer()=>Server;'+$integration.Substring($start,$end-$start)+'}'
$fixture=@"
public static class SpawnWitnessFixture{
 static int checks;
 static void Check(bool actual,bool expected,string name){if(actual!=expected)throw new Exception(name);checks++;}
 public static string Run(){
 string directory=Path.Combine(Path.GetTempPath(),"rebirth-spawn-witness-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);GameIO.Directory=directory;
 string path=Path.Combine(directory,"RebirthNpcWorldIntegration.xml");
 try{
 RebirthNpcPendingSpawn request,attempted;
 if(!RebirthNpcPendingSpawn.TryCreate("spawn:fixture",RebirthNpcStableId.NewId(),"survivor.ambient","npcRebirthSurvivorManSDCS",1,45,2,90,123,out request)||!request.TryMarkAttempted(out attempted))throw new Exception("setup");
 Func<RebirthNpcPendingSpawn,XElement> root=r=>new XElement("rebirthNpcWorldIntegration",new XAttribute("version",1),new XElement("pendingSpawns",new XAttribute("version",1),r.Write()));
 Check(WitnessHost.HasSavedPendingSpawn(request),false,"missing final");
 File.WriteAllText(path+".bak",root(request).ToString());Check(WitnessHost.HasSavedPendingSpawn(request),false,"backup is not final");
 File.WriteAllText(path,root(request).ToString());Check(WitnessHost.HasSavedPendingSpawn(request),true,"prepared final");
 Check(WitnessHost.HasSavedPendingSpawn(attempted),false,"phase mismatch");
 File.WriteAllText(path,root(attempted).ToString());Check(WitnessHost.HasSavedPendingSpawn(attempted),true,"attempted final");
 var image=root(attempted);image.Add(new XElement("replays",new XAttribute("version",1),new XElement("replay",new XAttribute("key",attempted.ReplayKey))));File.WriteAllText(path,image.ToString());Check(WitnessHost.HasSavedPendingSpawn(attempted),false,"completed request");
 image=root(request);image.Add(new XElement("identity",new XAttribute("stableId","bad")));File.WriteAllText(path,image.ToString());Check(WitnessHost.HasSavedPendingSpawn(request),false,"invalid other section");
 image=root(request);image.SetAttributeValue("unknown","bad");File.WriteAllText(path,image.ToString());Check(WitnessHost.HasSavedPendingSpawn(request),false,"invalid root");
 File.WriteAllText(path,root(request).ToString());WitnessHost.Server=false;Check(WitnessHost.HasSavedPendingSpawn(request),false,"client authority");WitnessHost.Server=true;
 File.WriteAllText(path,"<!DOCTYPE rebirthNpcWorldIntegration [<!ENTITY x 'bad'>]>"+root(request).ToString());Check(WitnessHost.HasSavedPendingSpawn(request),false,"DTD");
 }finally{WitnessHost.Server=true;foreach(string file in new[]{path,path+".bak"})if(File.Exists(file))File.Delete(file);Directory.Delete(directory,false);}
 return "PASS "+checks+" extracted production saved-intent witness checks with real files and authority/save-directory doubles; native world not exercised";
 }
}
"@
Add-Type -TypeDefinition ($source+$fixture)
[SpawnWitnessFixture]::Run()