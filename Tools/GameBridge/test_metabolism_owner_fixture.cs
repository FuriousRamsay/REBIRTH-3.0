using System;
public struct RebirthMetabolismSnapshot { public int OwnerEntityId;public long Sequence;public string CreationId;public float Food,Hydration; }
public class EntityPlayer { public World world;public int entityId;public Stats Stats=new Stats(); }
public class EntityPlayerLocal:EntityPlayer {}
public class Stats { public Stat Food=new Stat(),Water=new Stat(); }
public class Stat { public float Value,ModifiedMax=100;public bool Changed=true; }
public class World { public bool Remote=true,Throw;public EntityPlayerLocal Player;public EntityPlayer GetEntity(int id){return Player?.entityId==id?Player:null;}public bool IsRemote(){return Remote;}public EntityPlayerLocal GetPrimaryPlayer(){if(Throw)throw new Exception();return Player;} }
// Explicit native thread and authenticated creation-hold boundaries.
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread(){return Main;}}
public static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayer p){return Held;}}
public class GameManager { public static GameManager Instance=new GameManager();public World World; }
public static class Mathf { public static float Clamp(float x,float a,float b){return Math.Max(a,Math.Min(b,x));} }
public static class RebirthSurvivorMode { public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;} }
public static class RebirthSurvivorClientState { public static string Id;public static string GetProjectedCreationId(EntityPlayer p){return Id;} }
public class Origin { public string CreationId; }
public class RebirthWorldCharacterRecord { public bool IsComplete=true;public Origin Origin=new Origin(); }
public static class RebirthWorldCharacterService { public static RebirthWorldCharacterRecord Record;public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return p!=null&&r!=null;} }
// PRODUCTION_SCOPE
// PRODUCTION_CLIENT
class Checks {
static void A(bool b,string m){if(!b)throw new Exception(m);}
static void Main(){var w=new World();var p=new EntityPlayerLocal{entityId=1,world=w};w.Player=p;GameManager.Instance.World=w;
string first=Guid.NewGuid().ToString("N"),second=Guid.NewGuid().ToString("N");RebirthSurvivorClientState.Id=first;
var s=new RebirthMetabolismSnapshot{OwnerEntityId=1,CreationId=first,Sequence=10,Food=40,Hydration=50};RebirthMetabolismClientState.Receive(s);RebirthMetabolismSnapshot got;
A(RebirthMetabolismClientState.TryGet(out got)&&p.Stats.Food.Value==40,"current owner apply");
RebirthSurvivorClientState.Id=second;A(!RebirthMetabolismClientState.TryGet(out got)&&got.Sequence==0,"old character cache rejected");
s.Sequence=11;s.Food=1;RebirthMetabolismClientState.Receive(s);A(p.Stats.Food.Value==40,"old character delayed reserve rejected");
s.CreationId=second;s.Sequence=1;s.Food=60;RebirthMetabolismClientState.Receive(s);A(RebirthMetabolismClientState.TryGet(out got)&&p.Stats.Food.Value==60,"new character low sequence accepted");
s.Sequence=1;s.Food=2;RebirthMetabolismClientState.Receive(s);A(p.Stats.Food.Value==60,"duplicate refused");
w.Player=null;A(!RebirthMetabolismClientState.TryGet(out got)&&got.Sequence==0,"missing owner refused");w.Player=p;w.Throw=true;A(!RebirthMetabolismClientState.TryGet(out got),"exception refused");w.Throw=false;
s.Sequence=2;s.OwnerEntityId=2;RebirthMetabolismClientState.Receive(s);A(p.Stats.Food.Value==60,"other entity refused");
var nextWorld=new World();var nextPlayer=new EntityPlayerLocal{entityId=1,world=nextWorld};nextWorld.Player=nextPlayer;GameManager.Instance.World=nextWorld;
A(!RebirthMetabolismClientState.TryGet(out got),"same IDs in new world cannot expose old projection");
s.OwnerEntityId=1;s.Sequence=1;s.Food=70;RebirthMetabolismClientState.Receive(s);A(RebirthMetabolismClientState.TryGet(out got)&&got.Sequence==1,"new world lower sequence accepted");
var replacement=new EntityPlayerLocal{entityId=1,world=nextWorld};nextWorld.Player=replacement;A(!RebirthMetabolismClientState.TryGet(out got),"same-world replaced player cannot expose old projection");
RebirthCharacterCreationHoldService.Held=true;RebirthMetabolismClientState.Receive(s);A(!RebirthMetabolismClientState.TryGet(out got)&&replacement.Stats.Food.Value==0,"creation hold refuses reserves");
RebirthCharacterCreationHoldService.Held=false;nextWorld.Remote=false;RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord{Origin=new Origin{CreationId=second}};
RebirthMetabolismClientState.Receive(s);A(RebirthMetabolismClientState.TryGet(out got),"server-local direct publication preserved");
RebirthMetabolismClientState.Clear();A(!RebirthMetabolismClientState.TryGet(out got),"clear");Console.WriteLine("PASS: actual client projection/scope methods; current owner, profile replacement, delayed/duplicate/wrong-owner packets, missing player, exceptions and clear. Native networking/stats doubled.");}
}
