using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
namespace UnityEngine {public struct Vector3{public int x;}}
public enum RebirthBossEventSize{Small,Large,Medium}
public class World{}
public class EntityPlayer{public Vector3 position;}
public class RebirthBossEventProgressionSnapshot{public int EffectiveEventGameStage;public string OwnerStableId="owner";}
public enum RebirthSpawnSurface{Biome}
public enum RebirthSpawnProgressionMode{Biome,Gamestage}
public class RebirthSpawnContext{public RebirthSpawnSurface Surface;public RebirthSpawnProgressionMode ProgressionMode;public int GameStage;public string Biome,HistoryKey;}
public class RebirthSpawnTrace{public int EntityClassId;public string EntityName;}
public class EntityClass{public string entityClassName="zombie";public static EntityClass GetEntityClass(int id)=>new EntityClass();}
public class RebirthSandboxOptionManager{public static RebirthSandboxOptionManager Current=new();public bool IsPurge;}
public static class RebirthBossEventIdentity{public static string GetBiome(World world,Vector3 position)=>position.x>0?"desert":"forest";}
public static class RebirthBossEventDiagnostics{public static void Write(string s){}}
public static class RebirthSpawnCompositionService{public static readonly List<RebirthSpawnContext> Contexts=new();public static readonly List<string> Order=new();public static bool Fail;public static bool TrySelect(RebirthSpawnContext c,Func<double> rng,out RebirthSpawnTrace t){Order.Add("select");Contexts.Add(c);t=new RebirthSpawnTrace{EntityClassId=Contexts.Count};return !Fail;}public static bool IsForbiddenRestrictedSpawnEntity(EntityClass e)=>false;public static string GetStatus()=>"fixture";}
public static class RebirthBossEventPlacement{public static bool Fail;public static int Calls;public static bool TryBuildAtomicPlacement(World w,EntityPlayer p,int count,Random r,List<Vector3> positions,out Vector3 origin,out string failure){Calls++;RebirthSpawnCompositionService.Order.Add("place");origin=default;failure=Fail?"placement-failed":null;if(Fail)return false;for(int n=0;n<count;n++)positions.Add(new Vector3{x=n%2});return true;}}
class Program
{
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
 static void Reset(){RebirthSpawnCompositionService.Contexts.Clear();RebirthSpawnCompositionService.Order.Clear();RebirthSpawnCompositionService.Fail=false;RebirthBossEventPlacement.Fail=false;RebirthBossEventPlacement.Calls=0;}
 static void Main(){var w=new World();var owner=new EntityPlayer();RebirthBossEventSpawnPlan plan;string failure;
 RebirthSandboxOptionManager.Current.IsPurge=true;Reset();Check(RebirthBossEventCompositionResolver.TryBuild(w,owner,new RebirthBossEventProgressionSnapshot{EffectiveEventGameStage=100000},4,new Random(1),out plan,out failure),"Purge builds complete original boss/support/regular plan");
 Check(RebirthSpawnCompositionService.Order[0]=="place"&&RebirthBossEventPlacement.Calls==1,"Purge qualifies positions once before choosing composition");
 Check(RebirthSpawnCompositionService.Contexts.All(c=>c.ProgressionMode==RebirthSpawnProgressionMode.Biome&&c.GameStage==0),"boss regular and support selection ignore high player stage");
 Check(RebirthSpawnCompositionService.Contexts.Select(c=>c.Biome).SequenceEqual(new[]{"forest","desert","forest","desert","forest","desert","forest"}),"every role uses its actual individual spawn position biome");
 var signature=RebirthSpawnCompositionService.Contexts.Select(c=>c.Biome+":"+c.GameStage).ToArray();Reset();RebirthBossEventCompositionResolver.TryBuild(w,owner,new RebirthBossEventProgressionSnapshot{EffectiveEventGameStage=1},4,new Random(1),out plan,out failure);Check(signature.SequenceEqual(RebirthSpawnCompositionService.Contexts.Select(c=>c.Biome+":"+c.GameStage)),"low and high stage produce identical Purge composition inputs");
 Reset();RebirthBossEventPlacement.Fail=true;Check(!RebirthBossEventCompositionResolver.TryBuild(w,owner,new RebirthBossEventProgressionSnapshot(),4,new Random(1),out plan,out failure)&&RebirthSpawnCompositionService.Contexts.Count==0,"unqualified placement never selects guessed owner-biome composition");
 RebirthSandboxOptionManager.Current.IsPurge=false;Reset();Check(RebirthBossEventCompositionResolver.TryBuild(w,owner,new RebirthBossEventProgressionSnapshot{EffectiveEventGameStage=150},4,new Random(1),out plan,out failure),"None original plan still succeeds");
 Check(RebirthSpawnCompositionService.Order.Last()=="place"&&RebirthBossEventPlacement.Calls==1&&RebirthSpawnCompositionService.Contexts.All(c=>c.GameStage==150&&c.ProgressionMode==RebirthSpawnProgressionMode.Gamestage),"None preserves original RNG ordering and stage policy");
 Console.WriteLine("RESULT "+checks+" PASS; exact production class extract, placement/entity/selector doubles; game not run.");}
}