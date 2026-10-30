#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$game='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die'
$prod=Join-Path $game 'Mods/zzz_REBIRTH__3_0'
$docs=Join-Path $repo '_Documentation/Purge'
$boss='Scripts/Modules/BossEvents/RebirthBossEvents.cs'
$pub='Scripts/Modules/BossEvents/RebirthBossNativePublication.cs'
if((Get-FileHash (Join-Path $prod $boss)).Hash-ne'CEF2407531238D44F51F01BEF77A043FD32185E81531765D7DDAD23130F59B42'-or(Get-FileHash (Join-Path $prod $pub)).Hash-ne'338F79404A2AF988ACE6439A7886D740CDC2DD8C68AE8A33FE91D401C7386F81'){throw 'Production baseline changed'}
function Extract($s,$marker){$a=$s.IndexOf($marker,[StringComparison]::Ordinal);if($a-lt0){throw $marker};$b=$s.IndexOf('{',$a);$arrow=$s.IndexOf('=>',$a);$semi=$s.IndexOf(';',$a);if($arrow-ge$a-and$arrow-lt$b-and$semi-lt$b){return $s.Substring($a,$semi-$a+1)};$n=0;$i=$b;do{if($s[$i]-eq'{'){$n++};if($s[$i]-eq'}'){$n--};$i++}while($n-gt0);$s.Substring($a,$i-$a)}
$old=[IO.File]::ReadAllText((Join-Path $prod $boss));$fixed=[IO.File]::ReadAllText((Join-Path $repo $boss))
$oldPub=[IO.File]::ReadAllText((Join-Path $prod $pub));$fixedPub=[IO.File]::ReadAllText((Join-Path $repo $pub))
if($fixed-cne$old.Replace('original.EntityClass<0','(EntityClass.list==null||EntityClass.GetEntityClass(original.EntityClass)==null)')){throw 'Unexpected BossEvents delta'}
if($fixedPub-cne$oldPub.Replace('entity.entityClass<0','(EntityClass.list==null||EntityClass.GetEntityClass(entity.entityClass)==null)')){throw 'Unexpected publication delta'}
$evidence=Join-Path $prod '_Documentation/PlayerReports_20261029/BossNative-20261029/spawn-audit.json'
$audit=Get-Content $evidence -Raw|ConvertFrom-Json
$rows=$audit.domains.bossOriginals.originals
if($rows.Count-ne7-or$rows[0].nativeClass-ne-2076374061){throw 'Unexpected native evidence'}
$sourcePaths=@((Join-Path $prod $boss),(Join-Path $repo $boss),(Join-Path $prod $pub),(Join-Path $repo $pub),$evidence,(Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
$guards=@($sourcePaths|ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash $_).Hash}})
Add-Type -Path (Join-Path $game 'Mods/0_TFP_Harmony/Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
$il=@();foreach($m in ($assembly.MainModule.Types|Where-Object Name -eq EntityClass).Methods|Where-Object Name -in 'GetEntityClass','FromString'){$il+=$m.FullName;$il+=@($m.Body.Instructions|ForEach-Object ToString)}
$il|Set-Content (Join-Path $docs 'SUB7_BOSS_SIGNED_NATIVE_20261029.il.txt');$assembly.Dispose()
$classes=($rows|ForEach-Object {$_.nativeClass.ToString([Globalization.CultureInfo]::InvariantCulture)})-join','
$models=($old.Substring($old.IndexOf('public enum RebirthBossEventRole'),$old.IndexOf('public static class RebirthBossEventOptions')-$old.IndexOf('public enum RebirthBossEventRole')))
$doubles=@'
public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
public struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public class EntityClass{public static Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>();public static EntityClass GetEntityClass(int id){list.TryGetValue(id,out var value);return value;}}
public enum EnumSpawnerSource:byte{Biome=1,StaticSpawner=2,Dynamic=3}public enum StreamModeRead{Persistency}
public class Reader:BinaryReader{public Reader():base(new MemoryStream()){}public void SetBaseStream(Stream s){active=new BinaryReader(s);}BinaryReader active;public override int ReadInt32()=>active.ReadInt32();public override float ReadSingle()=>active.ReadSingle();public override byte[] ReadBytes(int n)=>active.ReadBytes(n);}
public static class MemoryPools{public static Pool poolBinaryReader=new Pool();public class Pool{public Reader AllocSync(bool reset)=>new Reader();}}
public class NativeStat{public float Value;}public class NativeStats{public NativeStat Health=new NativeStat();}
public class EntityCreationData{public int id,entityClass;public Vector3 pos;public byte readFileVersion=38;public MemoryStream entityData;public NativeStats stats=new NativeStats();public void read(Reader r,StreamModeRead m){id=r.ReadInt32();entityClass=r.ReadInt32();pos=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());int n=r.ReadInt32();entityData=new MemoryStream(r.ReadBytes(n));}}
public class World{}public class Entity{public int entityClass;public World world;public bool dead;public bool IsDead()=>dead;}
'@
$tests=@'
public static class Tests{
static int count;static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);count++;Console.WriteLine("PASS "+label);}
static byte[] Frame(RebirthBossSpawnOriginal o){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(o.EntityId);w.Write(o.EntityClass);w.Write(o.Position.x);w.Write(o.Position.y);w.Write(o.Position.z);w.Write(9);w.Write((byte)3);w.Write(o.WorldTimeBorn);return s.ToArray();}}
static RebirthBossSpawnOriginal Original(int id,int type){var o=new RebirthBossSpawnOriginal{Operation=Guid.NewGuid(),EntityId=id,EntityClass=type,WorldTimeBorn=33199,Phase=1,Role=2,Position=new Vector3(1,2,3)};o.PreparedNative=Frame(o);return o;}
static RebirthBossEventInstance Event(params RebirthBossSpawnOriginal[] originals){var e=new RebirthBossEventInstance{EventId=Guid.NewGuid(),State=RebirthBossEventState.Spawning,NativeBatchPrepared=true,Options=new RebirthBossEventOptionSnapshot()};e.SpawnOriginals.AddRange(originals);return e;}
static byte[] Bytes(RebirthBossEventInstance e){using(var s=new MemoryStream()){using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true))Persistence.Write(w,e);return s.ToArray();}}
static RebirthBossEventInstance Round(RebirthBossEventInstance e){using(var s=new MemoryStream(Bytes(e)))using(var r=new BinaryReader(s)){var result=Persistence.Read(r);if(s.Position!=s.Length)throw new Exception("not fully consumed");return result;}}
static void Reject(RebirthBossSpawnOriginal o,string label){try{Round(Event(o));}catch(Exception e)when(e is InvalidDataException||e is EndOfStreamException){Check(true,label);return;}Check(false,label);}
public static void Run(int[] ids,bool baseline){foreach(int id in ids)EntityClass.list[id]=new EntityClass();
if(baseline){try{Round(Event(Original(245,ids[0])));}catch(InvalidDataException){Console.WriteLine("EXPECTED BASELINE FAILURE negative real native class");return;}throw new Exception("Baseline unexpectedly accepted");}
var originals=new List<RebirthBossSpawnOriginal>();for(int i=0;i<ids.Length;i++)originals.Add(Original(245+i,ids[i]));var plan=Event(originals.ToArray());var copy=Round(plan);Check(copy.SpawnOriginals.Count==7,"seven actual observed signed-class originals roundtrip");Check(Bytes(plan).SequenceEqual(Bytes(copy)),"complete event bytes unchanged by write/read/write");
for(int i=0;i<ids.Length;i++){var a=originals[i];var b=copy.SpawnOriginals[i];Check(a.EntityClass==b.EntityClass&&a.EntityId==b.EntityId&&a.Operation==b.Operation&&b.Phase==1&&b.NativeProof==""&&a.PreparedNative.SequenceEqual(b.PreparedNative),"observed class/id/operation/attempted phase retained "+ids[i]);}
foreach(int id in new[]{int.MinValue,-1,0,int.MaxValue}){EntityClass.list[id]=new EntityClass();Check(Round(Event(Original(1,id))).SpawnOriginals[0].EntityClass==id,"registry identity accepts full signed range "+id);EntityClass.list.Remove(id);Reject(Original(1,id),"unknown registry class rejected "+id);}
var x=Original(245,ids[0]);x.EntityId=-1;Reject(x,"negative native entity ID remains rejected");x=Original(245,ids[0]);x.Operation=Guid.Empty;Reject(x,"empty operation rejected");x=Original(245,ids[0]);x.EntityClass=ids[1];Reject(x,"known different class from prepared native rejected");x=Original(245,ids[0]);x.WorldTimeBorn++;Reject(x,"wrong native birth rejected");x=Original(245,ids[0]);x.Position.x++;Reject(x,"wrong native position rejected");x=Original(245,ids[0]);x.Phase=3;Reject(x,"published phase still requires proof");x=Original(245,ids[0]);x.NativeProof="bad";Reject(x,"malformed proof rejected");x=Original(245,ids[0]);x.Role=5;Reject(x,"unknown role rejected");x=Original(245,ids[0]);x.Phase=4;Reject(x,"unknown phase rejected");x=Original(245,ids[0]);x.PreparedNative=x.PreparedNative.Concat(new byte[]{1}).ToArray();Reject(x,"trailing native bytes rejected");x=Original(245,ids[0]);x.NativeRemoved=true;Reject(x,"unproved native removal rejected");x=Original(245,ids[0]);x.DeathPublished=true;Reject(x,"unproved death rejected");
var world=new World();foreach(int id in ids)Check(!Persistence.AdoptionReject(new Entity{entityClass=id,world=world},world),"legacy adoption accepts known signed class "+id);Check(Persistence.AdoptionReject(new Entity{entityClass=1234567,world=world},world),"legacy adoption rejects unknown positive class");Check(Persistence.AdoptionReject(new Entity{entityClass=-1234567,world=world},world),"legacy adoption rejects unknown negative class");Check(Persistence.AdoptionReject(new Entity{entityClass=ids[0],world=world,dead=true},world),"legacy dead gate preserved");Check(Persistence.AdoptionReject(new Entity{entityClass=ids[0],world=new World()},world),"legacy world gate preserved");
EntityClass.list=null;Reject(Original(245,ids[0]),"uninitialized class registry refused");Check(Persistence.AdoptionReject(new Entity{entityClass=ids[0],world=world},world),"legacy uninitialized registry refused");Console.WriteLine("TOTAL "+count+" PASS; actual event serialization/validation and native identity methods, ECD/registry doubles; native NOT RUN");}}
'@
$outputs=@()
foreach($mode in @('Baseline','Fixed')){
    $s=if($mode-eq'Baseline'){$old}else{$fixed};$publication=if($mode-eq'Baseline'){$oldPub}else{$fixedPub}
    $methods=@();foreach($marker in @('private static bool Finite(','private static int ReadCount(','private static void WriteEvent(','private static RebirthBossEventInstance ReadEvent(','private static void WriteVector3(','private static Vector3 ReadVector3(','private static void WriteVector3i(','private static Vector3i ReadVector3i(','private static void WriteProgression(','private static RebirthBossEventProgressionSnapshot ReadProgression(','private static void WriteOptions(','private static RebirthBossEventOptionSnapshot ReadOptions(')){$methods+=Extract $s $marker}
    $guard=[regex]::Match($publication,'if\((entity==null\|\|entity.IsDead\(\).*?)\)\{unknown=true;break;\}').Groups[1].Value
    if(!$guard){throw 'Adoption guard missing'}
    $native=@();foreach($marker in @('internal static bool ValidPrepared(','internal static bool ValidProof(','internal static bool NativeBirth(','private static void Skip(','internal static bool ValidDeadRemoval(','internal static string RemovedDeathProof(')){$native+=Extract $publication $marker}
    $source='using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Security.Cryptography;namespace Signed'+$mode+'{'+$models+$doubles+'internal static class RebirthBossNativePublication{'+($native-join"`n")+'}internal static class Persistence{private const int DefaultPersistedMinimumPlayerLevel=15;'+($methods-join"`n")+'internal static void Write(BinaryWriter w,RebirthBossEventInstance e)=>WriteEvent(w,e);internal static RebirthBossEventInstance Read(BinaryReader r)=>ReadEvent(r,9);internal static bool AdoptionReject(Entity entity,World world)=>'+$guard+';}'+$tests+'}'
    Add-Type -TypeDefinition $source
    $type=[type]('Signed'+$mode+'.Tests')
    # Console output is redirected into the durable bounded receipt.
    $writer=[IO.StringWriter]::new();$previous=[Console]::Out
    try{[Console]::SetOut($writer);$type::Run([int[]]($rows|ForEach-Object nativeClass),($mode-eq'Baseline'))}finally{[Console]::SetOut($previous)}
    $outputs+=$mode+':';$outputs+=$writer.ToString()
}
foreach($guard in $guards){if((Get-FileHash $guard.path).Hash-ne$guard.sha256){throw 'Inputs changed'}}
$guards|ConvertTo-Json|Set-Content (Join-Path $docs 'SUB7_BOSS_SIGNED_GUARDS_20261029.json')
$outputs|Set-Content (Join-Path $docs 'SUB7_BOSS_SIGNED_CHECKS_20261029.txt')
$outputs|Select-Object -Last 1
