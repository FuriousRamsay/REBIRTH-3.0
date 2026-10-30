#requires -Version 7.0
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$prod='C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\zzz_REBIRTH__3_0'
$relative='Scripts/Modules/BossEvents/RebirthBossNativePublication.cs'
$boss='Scripts/Modules/BossEvents/RebirthBossEvents.cs'
if((Get-FileHash (Join-Path $prod $relative)).Hash-ne'6D06089F5233982D844227DCCAC8E1C8EA49486970A133D6200C63A39FEB0E50'-or(Get-FileHash (Join-Path $prod $boss)).Hash-ne'D55B2F3DB9C5EA6EC98625497F48DAB0FED10467CAE499EE9053D981B10693C0'){throw 'Production baseline changed'}
function Extract($s,$marker){$a=$s.IndexOf($marker,[StringComparison]::Ordinal);if($a-lt0){throw $marker};$b=$s.IndexOf('{',$a);$arrow=$s.IndexOf('=>',$a);$semi=$s.IndexOf(';',$a);if($arrow-ge$a-and$arrow-lt$b-and$semi-lt$b){return $s.Substring($a,$semi-$a+1)};$n=0;$i=$b;do{if($s[$i]-eq'{'){$n++};if($s[$i]-eq'}'){$n--};$i++}while($n-gt0);$s.Substring($a,$i-$a)}
$s=[IO.File]::ReadAllText((Join-Path $repo $relative));$old=[IO.File]::ReadAllText((Join-Path $prod $relative));$events=[IO.File]::ReadAllText((Join-Path $repo $boss));$oldEvents=[IO.File]::ReadAllText((Join-Path $prod $boss))
$helper=Extract $s 'internal static bool RemoveOriginal('
$expected=$old.Replace('    internal static void ObserveRemoved(',"    $helper`n    internal static void ObserveRemoved(")
# Line endings differ only at insertion, so compare after normalizing CRLF.
$expected=$expected.Replace('if(Matches(world,e,original,live)&&RebirthBossEventPersistence.Checkpoint())world.RemoveEntity(live.entityId,EnumRemoveEntityReason.Despawned);','RemoveOriginal(world,e,original,live);')
if($expected.Replace("`r`n","`n")-cne$s.Replace("`r`n","`n")){throw 'Unexpected publication source delta'}
$expectedEvents=$oldEvents.Replace('if(entity!=null&&RebirthBossNativePublication.BeginRemoval(world,e,original,entity))world.RemoveEntity(entity.entityId,EnumRemoveEntityReason.Despawned);','if(entity!=null)RebirthBossNativePublication.RemoveOriginal(world,e,original,entity);')
$expiry='if(original!=null&&!RebirthBossNativePublication.BeginRemoval(world,e,original,crate))return;'+"`n"+'            world.RemoveEntity('+"`n"+'                crate.entityId,'+"`n"+'                EnumRemoveEntityReason.Despawned);'
$expectedEvents=$expectedEvents.Replace("`r`n","`n").Replace($expiry,'if(!RebirthBossNativePublication.RemoveOriginal(world,e,original,crate))return;')
if($expectedEvents-cne$events.Replace("`r`n","`n")){throw 'Unexpected BossEvents source delta'}
$game=Split-Path (Split-Path $prod)
$paths=@((Join-Path $prod $relative),(Join-Path $repo $relative),(Join-Path $prod $boss),(Join-Path $repo $boss),(Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'),(Join-Path $prod 'Config/entityclasses.xml'),(Join-Path $game 'Data/Config/entityclasses.xml'),(Join-Path $prod 'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.cs'),(Join-Path $prod 'Scripts/Modules/BossEvents/RebirthBossPublicationCapture.cs'))
$guards=@($paths|ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash}})
Add-Type -Path (Join-Path $game 'Mods/0_TFP_Harmony/Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
try{
    $worldType=$assembly.MainModule.Types|Where-Object Name -eq World
    $remove=$worldType.Methods|Where-Object {$_.Name-eq'RemoveEntity'-and$_.Parameters.Count-eq2}
    $calls=@($remove.Body.Instructions|Where-Object {$_.Operand-is[Mono.Cecil.MethodReference]}|ForEach-Object {$_.Operand.FullName})
    if($calls.IndexOf('System.Void Entity::MarkToUnload()')-lt0-or$calls.IndexOf('System.Void Entity::MarkToUnload()')-ge$calls.IndexOf('System.Void World::unloadEntity(Entity,EnumRemoveEntityReason)')){throw 'Native mark/remove contract drift'}
    $container=$assembly.MainModule.Types|Where-Object Name -eq EntityContainerAbs
    $marked=$container.Methods|Where-Object Name -eq IsMarkedForUnload
    if(!@($marked.Body.Instructions|Where-Object {$_.Operand.FullName-eq'System.Boolean EntityContainerAbs::bRemoved'}).Count){throw 'Native container unload override drift'}
    $retire=$container.Methods|Where-Object Name -eq removeBackpack
    if(!$retire.IsPublic-or!@($retire.Body.Instructions|Where-Object {$_.OpCode.Name-eq'stfld'-and$_.Operand.FullName-eq'System.Boolean EntityContainerAbs::bRemoved'}).Count){throw 'Native public container retirement drift'}
    $loot=$assembly.MainModule.Types|Where-Object Name -eq EntityLootContainer
    if($loot.BaseType.Name-ne'EntityContainerAbs'-or@($loot.Methods|Where-Object {$_.Name-in@('onBackpackRemoved','MarkToUnload','IsMarkedForUnload')}).Count){throw 'Native reward type/override changed'}
}finally{$assembly.Dispose()}
$methods=@();foreach($marker in @('internal static bool BeginRemoval(','internal static bool RemoveOriginal(','private static bool OriginalCurrent(','internal static bool Matches(','private static bool AliveIdentity(','internal static string Key(','internal static byte[] Stamp(')){$methods+=Extract $s $marker}
foreach($marker in @('internal static bool BeginRemoval(','private static bool OriginalCurrent(','internal static bool Matches(','private static bool AliveIdentity(','internal static string Key(','internal static byte[] Stamp(')){if((Extract $s $marker)-cne(Extract $old $marker)){throw ('Unchanged method drift '+$marker)}}
$baseline=[regex]::Match($oldEvents,'if\(entity!=null&&RebirthBossNativePublication.BeginRemoval\(world,e,original,entity\)\)world.RemoveEntity\(entity.entityId,EnumRemoveEntityReason.Despawned\);').Value
if(!$baseline){throw 'Actual old cancel dispatch not found'}
$models=(Extract $events 'public sealed class RebirthBossSpawnOriginal')
$doubles=@'
public struct Vector3{public float x,y,z;}public struct Vector3i{public int x,y,z;}
public class RebirthBossEventInstance{public Guid EventId=Guid.NewGuid();public Progression Progression=new Progression();public List<RebirthBossSpawnOriginal> SpawnOriginals=new List<RebirthBossSpawnOriginal>();}public class Progression{public string OwnerStableId="owner";}
public static class RebirthBossEventRegistry{public static RebirthBossEventInstance Current;public static bool TryGet(Guid id,out RebirthBossEventInstance e){e=Current;return e!=null&&e.EventId==id;}}
public static class RebirthBossEventPersistence{public static bool IsReady=true,Allow=true;public static int Calls;public static List<string> Order=new List<string>();public static bool Checkpoint(){Calls++;Order.Add("checkpoint");return Allow;}}
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class GameManager{public static Manager Instance=new Manager();public class Manager{public World World;}}
public class ChunkCustomData{public byte[] data;public ulong expiresInWorldTime=ulong.MaxValue;public bool isSavedToNetwork;public object TriggerWriteDataDelegate;}
public class Chunk{public Custom ChunkCustomData=new Custom();public class Custom{public Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();}public bool InProgressUnloading;public List<Entity> entities=new List<Entity>();public void RemoveEntityFromChunk(Entity e){entities.Remove(e);}}
public enum EnumRemoveEntityReason{Despawned}
public class World{public State worldState=new State();public class State{public string Guid="world";}public bool remote;public Chunk chunk=new Chunk();public Dictionary<int,Entity> entities=new Dictionary<int,Entity>();public bool IsRemote()=>remote;public object GetChunkSync(int x,int z)=>chunk;public object GetChunkFromWorldPos(int x,int z)=>chunk;public Entity GetEntity(int id){entities.TryGetValue(id,out var e);return e;}public Entity RemoveEntity(int id,EnumRemoveEntityReason reason){var e=GetEntity(id);if(e==null)return null;e.MarkToUnload();entities.Remove(id);if(e.addedToChunk&&e.IsMarkedForUnload()&&!chunk.InProgressUnloading)chunk.RemoveEntityFromChunk(e);RebirthBossEventPersistence.Order.Add("native-remove");return e;}}
public class Entity{public World world;public int entityId=261,entityClass=764755902;public ulong WorldTimeBorn=33276;public bool addedToChunk=true,markedForUnload;public Vector3 position;public Vector3i chunkPosAddedEntityTo;public virtual void MarkToUnload(){markedForUnload=true;}public virtual bool IsMarkedForUnload()=>markedForUnload;}
public class Buffs{Dictionary<string,float> vars=new Dictionary<string,float>();public float GetCustomVar(string k)=>vars.TryGetValue(k,out var v)?v:0;public void SetCustomVar(string k,float v){vars[k]=v;}}
public class EntityAlive:Entity{public Buffs Buffs=new Buffs();public int deathUpdateTime,timeStayAfterDeath=100;public override void MarkToUnload(){base.MarkToUnload();deathUpdateTime=timeStayAfterDeath;}public override bool IsMarkedForUnload()=>base.IsMarkedForUnload()&&deathUpdateTime>=timeStayAfterDeath;}
public class EntityClass{public static int FromString(string name)=>764755902;}public class EntityContainerAbs:Entity{}public class EntityBackpack:EntityContainerAbs{}public class ForeignLoot:EntityLootContainer{}public class EntityLootContainer:EntityContainerAbs{public bool bRemoved;public int calls;public void removeBackpack(string reason){calls++;RebirthBossEventPersistence.Order.Add("container-retire");bRemoved=true;MarkToUnload();}public override bool IsMarkedForUnload()=>base.IsMarkedForUnload()&&bRemoved;}
'@
$tests=@'
public static class Tests{static int count;static void Check(bool v,string label){if(!v)throw new Exception("FAIL "+label);count++;Console.WriteLine("PASS "+label);}
class Setup{public World w=new World();public RebirthBossEventInstance e=new RebirthBossEventInstance();public RebirthBossSpawnOriginal o;public Entity entity;public Setup(bool reward=true){RebirthBossEventPersistence.IsReady=true;RebirthBossEventPersistence.Allow=true;RebirthBossEventPersistence.Calls=0;RebirthBossEventPersistence.Order.Clear();ThreadManager.Main=true;GameManager.Instance.World=w;RebirthBossEventRegistry.Current=e;entity=reward?(Entity)new EntityLootContainer():new EntityAlive();entity.world=w;o=new RebirthBossSpawnOriginal{Operation=Guid.NewGuid(),EntityId=entity.entityId,EntityClass=entity.entityClass,WorldTimeBorn=entity.WorldTimeBorn,Role=reward?3:0,Phase=3,NativeProof=new string('a',64)};e.SpawnOriginals.Add(o);w.entities[entity.entityId]=entity;w.chunk.entities.Add(entity);w.chunk.ChunkCustomData.dict[RebirthBossNativePublication.Key(o)]=new ChunkCustomData{data=RebirthBossNativePublication.Stamp(w,e,o)};}}
static bool Remove(Setup s)=>RebirthBossNativePublication.RemoveOriginal(s.w,s.e,s.o,s.entity);
static void Refuse(Action<Setup> change,string label){var s=new Setup();change(s);Check(!Remove(s),label+" refused");Check(!((EntityLootContainer)s.entity).bRemoved&&s.w.chunk.entities.Contains(s.entity),label+" no native custody mutation");}
public static void Run(){var b=new Setup();RebirthBossNativePublication.Baseline(b.w,b.e,b.o,b.entity);Check(b.w.GetEntity(b.o.EntityId)==null&&b.w.chunk.entities.Contains(b.entity)&&!((EntityLootContainer)b.entity).bRemoved,"actual baseline dispatch reproduces globally absent persisted-container custody");var s=new Setup();Check(Remove(s),"exact original reward removal dispatched");Check(!s.w.chunk.entities.Contains(s.entity)&&s.w.GetEntity(s.o.EntityId)==null,"native container contract removes chunk and global custody");Check(((EntityLootContainer)s.entity).calls==1,"native public retirement called once");Check(string.Join(",",RebirthBossEventPersistence.Order)=="checkpoint,container-retire,native-remove","intent checkpoint before container retirement before world removal");Check(s.o.RemovalInvoking&&!s.o.NativeRemoved&&s.o.RemovalProof==""&&s.o.NativeProof==new string('a',64),"effect never invents absence receipt or changes native proof");Check(!Remove(s)&&((EntityLootContainer)s.entity).calls==1,"already absent identity not retired again");
s=new Setup(false);Check(Remove(s)&&s.w.chunk.entities.Count==0,"ordinary alive regular follows existing virtual native mark contract");Check(string.Join(",",RebirthBossEventPersistence.Order)=="checkpoint,native-remove","ordinary regular never uses container retirement");
Refuse(x=>x.w.entities[x.o.EntityId]=new EntityLootContainer{world=x.w},"foreign registered object");Refuse(x=>x.o.Role=4,"nonreward container original");Refuse(x=>RebirthBossEventPersistence.Allow=false,"failed intent checkpoint");Refuse(x=>x.entity.entityClass++,"wrong class");Refuse(x=>x.entity.entityId++,"wrong ID");Refuse(x=>x.entity.WorldTimeBorn++,"wrong birth");Refuse(x=>x.entity.world=new World(),"foreign entity world");Refuse(x=>GameManager.Instance.World=new World(),"foreign current world");Refuse(x=>x.w.remote=true,"remote world");Refuse(x=>ThreadManager.Main=false,"wrong thread");Refuse(x=>RebirthBossEventPersistence.IsReady=false,"unready persistence");Refuse(x=>x.entity.addedToChunk=false,"unloaded original");Refuse(x=>x.o.Phase=2,"unpublished original");Refuse(x=>x.o.NativeRemoved=true,"already acknowledged original");Refuse(x=>RebirthBossEventRegistry.Current=new RebirthBossEventInstance(),"foreign event registry");Refuse(x=>x.e.SpawnOriginals.Clear(),"retired original membership");Refuse(x=>x.w.chunk.ChunkCustomData.dict.Clear(),"missing native stamp");
s=new Setup();((EntityLootContainer)s.entity).bRemoved=true;Check(Remove(s)&&((EntityLootContainer)s.entity).calls==0&&s.w.chunk.entities.Count==0,"already natively retired loaded crate not retired twice");
foreach(Entity other in new Entity[]{new EntityBackpack(),new ForeignLoot()}){s=new Setup();other.world=s.w;other.entityId=s.o.EntityId;other.entityClass=s.o.EntityClass;other.WorldTimeBorn=s.o.WorldTimeBorn;s.w.entities[other.entityId]=other;s.w.chunk.entities.Clear();s.w.chunk.entities.Add(other);s.entity=other;Check(!Remove(s)&&s.w.chunk.entities.Contains(other)&&RebirthBossEventPersistence.Calls==0,"foreign container/native subtype rejected before checkpoint "+other.GetType().Name);}Console.WriteLine("TOTAL "+count+" PASS; actual Boss methods and exact native-contract doubles; no game/native acceptance");}}
'@
$source='using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;namespace BossContainerControl{'+$models+$doubles+'internal static class RebirthBossNativePublication{'+($methods-join"`n")+'internal static void Baseline(World world,RebirthBossEventInstance e,RebirthBossSpawnOriginal original,Entity entity){'+$baseline+'}}'+$tests+'}'
Add-Type -TypeDefinition $source
$writer=[IO.StringWriter]::new();$previous=[Console]::Out
try{[Console]::SetOut($writer);[BossContainerControl.Tests]::Run()}finally{[Console]::SetOut($previous)}
$writer.ToString()|Set-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_CONTAINER_CHECKS_20261029.txt')
$writer.ToString()
# Compile the actual selected helper/identity methods against real native/Unity
# references, with only domain registry/persistence shells. Never execute it.
$apiDomain=@'
public sealed class RebirthBossEventInstance{public Guid EventId;public Progression Progression;public List<RebirthBossSpawnOriginal> SpawnOriginals=new List<RebirthBossSpawnOriginal>();}public class Progression{public string OwnerStableId;}
public static class RebirthBossEventRegistry{public static bool TryGet(Guid id,out RebirthBossEventInstance value){value=null;return false;}}
public static class RebirthBossEventPersistence{public static bool IsReady=>false;public static bool Checkpoint()=>false;}
'@
$api='using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using UnityEngine;namespace BossContainerNativeApi{'+$models+$apiDomain+'internal static class RebirthBossNativePublication{'+($methods-join"`n")+'}}'
$references=@(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter *.dll|ForEach-Object FullName)+@(Get-ChildItem (Join-Path $game '7DaysToDie_Data/Managed') -Filter *.dll|Where-Object {$_.Name-notmatch'^(System|Microsoft|mscorlib|netstandard)'}|ForEach-Object FullName)
Add-Type -TypeDefinition $api -ReferencedAssemblies $references
'PASS actual helper/identity native API typecheck; no execution/mod build'|Set-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_CONTAINER_API_20261029.txt')
$dotnetRoot=Split-Path (Get-Command dotnet).Source
$sdk=Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory|Where-Object {$_.Name-like'9.*'}|Sort-Object Name|Select-Object -Last 1
Add-Type -Path (Join-Path $sdk.FullName 'Roslyn/bincore/Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $sdk.FullName 'Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll')
foreach($whole in @($s,$events)){if(@([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($whole).GetDiagnostics()|Where-Object Severity -eq Error).Count){throw 'Whole caller syntax error'}}
'PASS whole caller syntax and deterministic three callsite delta'|Add-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_CONTAINER_API_20261029.txt')
foreach($guard in $guards){if((Get-FileHash -LiteralPath $guard.path).Hash-ne$guard.sha256){throw 'Source changed during verification'}}
$guards|ConvertTo-Json|Set-Content (Join-Path $repo '_Documentation/Purge/SUB7_BOSS_CONTAINER_GUARDS_20261029.json')
