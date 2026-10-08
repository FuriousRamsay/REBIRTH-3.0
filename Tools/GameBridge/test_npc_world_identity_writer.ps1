#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs'))
$start=$source.IndexOf('    private static void SaveNoLock()')
$end=$source.IndexOf('    public static void ResetForWorldChange()', $start)
if($start-lt0-or$end-lt0){throw 'Production writer extraction failed'}
$prefix=@"
using System.Linq;using System.Xml.Linq;using System;using System.IO;using System.Xml;using System.Text;using System.Globalization;using System.Collections.Generic;
public static class GameIO{public static string Directory;public static string GetSaveGameDir()=>Directory;}
public static class RebirthNpcPersistenceFile{public static bool Block;public static void AssertWritable(string path){if(Block)throw new IOException("blocked");}}
public class Identity{public Guid StableId;public int AmbientEntityId;public string ProfileId,DisplayName;public long PromotedUtcTicks;}
public static class ActualWorldWriterFixture{
 const int SchemaVersion=1;const string FileName="RebirthNpcWorldIntegration.xml";
 static Dictionary<Guid,Identity> ByStable=new Dictionary<Guid,Identity>();
static Dictionary<string,RebirthNpcPendingSpawn> PendingSpawns=new Dictionary<string,RebirthNpcPendingSpawn>();
 static HashSet<string> Replay=new HashSet<string>{"spawn:fixture","promote:fixture"};
 public static void Save(string directory,string name){GameIO.Directory=directory;ByStable.Clear();var id=Guid.Parse("11111111-1111-1111-1111-111111111111");ByStable[id]=new Identity{StableId=id,AmbientEntityId=42,ProfileId="survivor.persistent",DisplayName=name,PromotedUtcTicks=123};SaveNoLock();}
"@
$codec=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnReplayCodec.cs')).Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Xml;','')
$foundation=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'))
$idStart=$foundation.IndexOf('public readonly struct RebirthNpcStableId');$idEnd=$foundation.IndexOf('public sealed class RebirthNpcProfile',$idStart)
if($idStart-lt0-or$idEnd-lt0){throw 'Native identity extraction failed'}
$extras=$foundation.Substring($idStart,$idEnd-$idStart)
foreach($file in @('RebirthNpcPendingSpawn.cs','RebirthNpcPendingSpawnPersistence.cs')){
 $extras+=[regex]::Replace([IO.File]::ReadAllText((Join-Path $taskRoot ('Scripts/NPC/WorldIntegration/'+$file))),'(?m)^using [^\r\n]+;\r?\n','')
}
Add-Type -TypeDefinition ($prefix+$source.Substring($start,$end-$start)+"`n}"+$codec+$extras)
$directory=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-world-writer-'+[guid]::NewGuid().ToString('N'))
$path=Join-Path $directory 'RebirthNpcWorldIntegration.xml'
$checks=0
function Assert($condition,$reason){if(!$condition){throw $reason};$script:checks++}
try{
 [ActualWorldWriterFixture]::Save($directory,'Before')
 [xml]$first=[IO.File]::ReadAllText($path)
 Assert ($first.rebirthNpcWorldIntegration.identity.displayName-eq'Before') 'Initial save invalid'
 Assert (@($first.rebirthNpcWorldIntegration.replays.replay).Count-eq2) 'Replay completion records missing'
 [ActualWorldWriterFixture]::Save($directory,'After')
 [xml]$final=[IO.File]::ReadAllText($path);[xml]$backup=[IO.File]::ReadAllText($path+'.bak')
 Assert ($final.rebirthNpcWorldIntegration.identity.displayName-eq'After'-and$backup.rebirthNpcWorldIntegration.identity.displayName-eq'Before') 'Atomic replacement/backup invalid'
 Assert (![IO.File]::Exists($path+'.tmp')) 'Successful replacement retained temporary'
 $threw=$false;try{[ActualWorldWriterFixture]::Save('','Bad')}catch{$threw=$true}
 Assert $threw 'Empty directory silently succeeded'
 [RebirthNpcPersistenceFile]::Block=$true;$threw=$false;try{[ActualWorldWriterFixture]::Save($directory,'Blocked')}catch{$threw=$true};[RebirthNpcPersistenceFile]::Block=$false
 Assert ($threw-and[IO.File]::ReadAllText($path).Contains('After')) 'Blocked writer replaced final'
 $lock=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
 $threw=$false;try{[ActualWorldWriterFixture]::Save($directory,'Locked')}catch{$threw=$true}finally{$lock.Dispose()}
 Assert ($threw-and[IO.File]::ReadAllText($path).Contains('After')) 'Failed replacement damaged final'
 [ActualWorldWriterFixture]::Save($directory,'Retried')
 Assert ([IO.File]::ReadAllText($path).Contains('Retried')) 'Replacement retry failed'
 $result="PASS $checks actual extracted world-identity writer checks using real temporary files; native save/checkpoint/registry not exercised"
}finally{
 foreach($file in @($path,($path+'.tmp'),($path+'.bak'))){if([IO.File]::Exists($file)){[IO.File]::Delete($file)}}
 if([IO.Directory]::Exists($directory)){[IO.Directory]::Delete($directory,$false)}
}
$result
