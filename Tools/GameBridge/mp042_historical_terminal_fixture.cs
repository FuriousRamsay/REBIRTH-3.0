// Actual original plan encoding and historical metadata retirement. Native
// identity/body/world and warm-attempt containers are explicit adapters.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
class ItemStack {internal string Rich="original";internal int count=1;internal bool IsEmpty()=>count==0;}
static class RebirthCamperInventorySnapshot {internal static byte[] Capture(ItemStack[] items){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(items.Length);foreach(var item in items){w.Write(item.Rich);w.Write(item.count);}w.Flush();return m.ToArray();}}}
class EntityPlayerLocal {internal int entityId=1,Died=3;internal World world;internal string Owner="owner",Creation="creation";internal bool Dead,Spawned=true;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class World {internal State worldState=new State();internal EntityPlayerLocal Primary,Registered;internal class State {internal string Guid=System.Guid.NewGuid().ToString("N");}internal object GetEntity(int id)=>Registered;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class GameIO {internal static string Directory="captured-fixture-directory";internal static string GetSaveGameDir()=>Directory;}
static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal p)=>p.Creation;}
class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayerLocal p,out RebirthStablePlayerIdentity i){i=new RebirthStablePlayerIdentity{CanonicalId=p.Owner};return p.Owner!=null;}}
internal static partial class RebirthCookingOwnerDelta
{
    // ACTUAL_PLAN
    // ACTUAL_ENCODING
    private sealed class Attempt {internal Plan Plan;internal string Image,Directory;internal int Deaths;}
    private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
    private static object permit;
    private static void Need(bool b,string why){if(!b)throw new Exception(why);}
    internal static void Exercise()
    {
        for(int variant=0;variant<18;variant++)
        {
            Attempts.Clear();permit=null;GameIO.Directory="captured-fixture-directory";var world=new World();var player=new EntityPlayerLocal{world=world};world.Primary=world.Registered=player;GameManager.Instance.World=world;
            var plan=new Plan{Operation=Guid.NewGuid(),World=world.worldState.Guid,Owner="owner",Creation="creation",Deaths=2,SourceHash=new string('a',64),BeforeBag=new[]{new ItemStack()},AfterBag=new[]{new ItemStack{count=2}},BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};
            var attempt=new Attempt{Plan=plan,Image=Encode(plan),Directory=GameIO.Directory,Deaths=2};Attempts[plan.Operation]=attempt;bool authority=true;int reads=0;
            switch(variant)
            {
                case 1:Attempts.Clear();break;case 2:player.Died=2;break;case 3:player.Died=1;break;case 4:plan.Deaths=-1;break;case 5:player.Owner="foreign";break;
                case 6:player.Creation="foreign";break;case 7:plan.World=Guid.NewGuid().ToString("N");break;case 8:player.Dead=true;break;case 9:player.Spawned=false;break;
                case 10:world.Registered=new EntityPlayerLocal();break;case 11:authority=false;break;case 12:attempt.Image="changed";break;case 13:attempt.Directory="other-directory";break;
                case 14:attempt.Deaths=1;break;case 15:permit=new object();break;case 16:GameManager.Instance.World=new World();break;
            }
            bool Current(){reads++;if(variant==17&&reads==2)player.Died++;return authority;}
            bool result=RetireHistoricalMetadata(player,plan,Current);Need(result==(variant==0||variant==1),"historical metadata qualification incorrect "+variant);
            Need(Attempts.Count==((variant==0||variant==1)?0:1),"unqualified original metadata was discarded "+variant);
            if(result)Need(RetireHistoricalMetadata(player,plan,()=>true),"historical completion not idempotent");
        }
        Console.WriteLine("MP042 actual historical terminal metadata retirement: PASS (native identity/containers are adapters)");
    }
}
// ACTUAL_HISTORICAL
static class Program {public static void Main()=>RebirthCookingOwnerDelta.Exercise();}
