using System;
using System.Collections.Generic;
class TagGroup {public class Global {}}
struct FastTags<T> {public bool Zombie;public static FastTags<T> Parse(string s)=>new(){Zombie=s=="zombie"};public bool Test_AnySet(FastTags<T> other)=>Zombie&&other.Zombie;}
class EntityClass {
 public bool bIsAnimalEntity;public FastTags<TagGroup.Global> Tags;public string entityClassName;
 public static Dictionary<int,EntityClass> All=new();
 public static EntityClass GetEntityClass(int id)=>All.TryGetValue(id,out var c)?c:null;
 public static int FromString(string name)=>1;
}
enum RebirthSpawnSurface {Sleeper,WanderingHorde,EventSpawn,Ambient}
class RebirthSpawnContext {public RebirthSpawnSurface Surface;public bool DeferCompositionUntilPosition;public object ProgressionMode,GameStage,Biome,HistoryKey;public string RequestedGroup,PrefabName;}
class RebirthSpawnCompositionRuntimeIntegration {public static RebirthSpawnContext Current;}
class GameRandom {public float RandomFloat=>0.5f;}
class World {public GameRandom GetGameRandom()=>new();}
class GameManager {public static GameManager Instance;public World World;}
class RebirthSpawnTrace {public int EntityClassId;public string EntityName;}
class RebirthSpawnCompositionService {
 public static bool Available=true;public static int Selected=1;
 public static bool IsRestrictedNoSpecialSurface(RebirthSpawnSurface s)=>s!=RebirthSpawnSurface.Ambient;
 public static bool IsForbiddenRestrictedSpawnEntity(EntityClass c)=>false;
 public static bool IsForbiddenRestrictedSpawnEntity(int id)=>false;
 public static bool TrySelect(RebirthSpawnContext c,Func<double> random,out RebirthSpawnTrace trace){trace=new(){EntityClassId=Selected,EntityName="zombieArlene"};return Available;}
}
class Log {public static void Out(string s){}}
class Program {
 static int checks;
 static void Run(string name,RebirthSpawnSurface surface,int original,int expected,bool available=true,int selected=1) {
 RebirthSpawnCompositionRuntimeIntegration.Current=new(){Surface=surface,PrefabName="farm_05"};
 RebirthSpawnCompositionService.Available=available;RebirthSpawnCompositionService.Selected=selected;
 int result=original,last=original;Resolver.Postfix("animalGroup",ref last,new GameRandom(),ref result);
 if(result!=expected||last!=expected)throw new Exception(name+" result="+result);checks++;Console.WriteLine("PASS "+name);
 }
 static void Main(){
 EntityClass.All[1]=new(){entityClassName="zombieArlene"};
 EntityClass.All[2]=new(){bIsAnimalEntity=true,entityClassName="animalChicken"};
 EntityClass.All[3]=new(){bIsAnimalEntity=true,entityClassName="animalBoar"};
 EntityClass.All[4]=new(){bIsAnimalEntity=true,entityClassName="animalSnake"};
 EntityClass.All[5]=new(){bIsAnimalEntity=true,entityClassName="animalZombieBear",Tags=FastTags<TagGroup.Global>.Parse("zombie")};
 Run("sleeper chicken replaced",RebirthSpawnSurface.Sleeper,2,1);
 Run("sleeper boar replaced",RebirthSpawnSurface.Sleeper,3,1);
 Run("sleeper snake replaced",RebirthSpawnSurface.Sleeper,4,1);
 Run("ambient wildlife preserved",RebirthSpawnSurface.Ambient,2,2);
 Run("zombie animal preserved",RebirthSpawnSurface.Sleeper,5,5);
 Run("pool unavailable falls back to zombie",RebirthSpawnSurface.Sleeper,2,1,false);
 Run("animal returned by replacement pool rejected",RebirthSpawnSurface.Sleeper,2,1,true,3);
 Run("ordinary zombie preserved",RebirthSpawnSurface.Sleeper,1,1);
 Console.WriteLine(checks+" checks passed; actual production Postfix extracted, native dependencies doubled");
 }
}
